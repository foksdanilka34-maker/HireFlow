using System.Net;
using System.Net.Http.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Notifications;
using HireFlow.Domain.Notifications;

namespace HireFlow.Api.Tests;

public class NotificationApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    private Task<HttpResponseMessage> SentAsync(Guid id) =>
        Client.PostAsync($"/internal/notifications/{id}/sent", null);

    private Task<HttpResponseMessage> FailedAsync(Guid id, string error) =>
        Client.PostAsJsonAsync($"/internal/notifications/{id}/failed", new NotificationFailedRequest { Error = error }, Json);

    // Читает уведомление из БД.
    private async Task<Notification> ReloadAsync(Guid applicationId, Guid notificationId) =>
        (await GetNotificationsAsync(applicationId)).Single(n => n.Id == notificationId);

    // Создаёт приглашённый отклик: ожидающее уведомление, которое сразу пора отправлять.
    private async Task<(Guid ApplicationId, Notification Notification, long TelegramId, string Title)> InvitedAsync()
    {
        var telegramId = NewTelegramId();
        var title = Unique("Due ");
        var application = await CreateReviewApplicationAsync(title, telegramId);
        (await DecideAsync(application.Id, "invite")).EnsureSuccessStatusCode();
        return (application.Id, Assert.Single(await GetNotificationsAsync(application.Id)), telegramId, title);
    }

    [Fact]
    public async Task Due_ReturnsPendingNotificationWithTelegramIdTypeAndVacancyTitle()
    {
        var (_, notification, telegramId, title) = await InvitedAsync();

        var raw = await (await Client.GetAsync("/internal/notifications/due")).Content.ReadAsStringAsync();
        var due = System.Text.Json.JsonSerializer.Deserialize<List<DueNotificationResponse>>(raw, Json)!
            .Single(d => d.Id == notification.Id);

        // Тип в JSON пишется в snake_case: ищем именно элемент этого уведомления.
        using var json = System.Text.Json.JsonDocument.Parse(raw);
        var element = json.RootElement.EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == notification.Id);
        Assert.Equal("invited", element.GetProperty("type").GetString());
        Assert.Equal(telegramId, due.TelegramId);
        Assert.Equal(NotificationType.Invited, due.Type);
        Assert.Equal(title, due.VacancyTitle);
    }

    [Fact]
    public async Task Due_DoesNotReturnNotificationWithFutureSendAfter()
    {
        var application = await CreateAutoRejectedApplicationAsync();
        var notification = Assert.Single(await GetNotificationsAsync(application.Id));

        Assert.DoesNotContain(await GetDueAsync(), d => d.Id == notification.Id);

        await MakeDueAsync(notification.Id);

        Assert.Contains(await GetDueAsync(), d => d.Id == notification.Id);
    }

    [Fact]
    public async Task Sent_Returns204AndMarksNotificationSent()
    {
        var (applicationId, notification, _, _) = await InvitedAsync();

        var response = await SentAsync(notification.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var reloaded = await ReloadAsync(applicationId, notification.Id);
        Assert.Equal(NotificationStatus.Sent, reloaded.Status);
        Assert.NotNull(reloaded.SentAt);
        Assert.DoesNotContain(await GetDueAsync(), d => d.Id == notification.Id);
    }

    [Fact]
    public async Task Sent_RepeatedConfirmationReturns204AndKeepsSentAt()
    {
        var (applicationId, notification, _, _) = await InvitedAsync();
        (await SentAsync(notification.Id)).EnsureSuccessStatusCode();
        var firstSentAt = (await ReloadAsync(applicationId, notification.Id)).SentAt;

        var response = await SentAsync(notification.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var reloaded = await ReloadAsync(applicationId, notification.Id);
        Assert.Equal(NotificationStatus.Sent, reloaded.Status);
        Assert.Equal(firstSentAt, reloaded.SentAt);
    }

    [Fact]
    public async Task Sent_ForCancelledNotificationReturns409()
    {
        var application = await CreateAutoRejectedApplicationAsync();
        var rejection = Assert.Single(await GetNotificationsAsync(application.Id));
        (await DecideAsync(application.Id, "invite")).EnsureSuccessStatusCode();

        var response = await SentAsync(rejection.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(NotificationStatus.Cancelled, (await ReloadAsync(application.Id, rejection.Id)).Status);
    }

    [Fact]
    public async Task Sent_UnknownIdReturns404()
    {
        var response = await SentAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Failed_Returns204IncrementsAttemptsAndPostponesNotification()
    {
        var (applicationId, notification, _, _) = await InvitedAsync();
        var before = DateTime.UtcNow;

        var response = await FailedAsync(notification.Id, "chat not found");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var reloaded = await ReloadAsync(applicationId, notification.Id);
        Assert.Equal(NotificationStatus.Pending, reloaded.Status);
        Assert.Equal(1, reloaded.Attempts);
        Assert.Equal("chat not found", reloaded.LastError);
        Assert.InRange(reloaded.SendAfter, before.AddMinutes(2), DateTime.UtcNow.AddMinutes(2));
        Assert.DoesNotContain(await GetDueAsync(), d => d.Id == notification.Id);
    }

    [Fact]
    public async Task Failed_ForSentNotificationReturns409()
    {
        var (_, notification, _, _) = await InvitedAsync();
        (await SentAsync(notification.Id)).EnsureSuccessStatusCode();

        var response = await FailedAsync(notification.Id, "boom");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Failed_EmptyErrorReturns400AndChangesNothing()
    {
        var (applicationId, notification, _, _) = await InvitedAsync();

        var response = await FailedAsync(notification.Id, "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, (await ReloadAsync(applicationId, notification.Id)).Attempts);
    }
}
