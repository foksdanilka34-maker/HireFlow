using HireFlow.Bot.Api;
using HireFlow.Bot.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireFlow.Bot.Tests;

// Одна итерация рассылки: результат отправки уходит в API как sent или failed.
public class NotificationDispatcherTests
{
    private readonly FakeHireFlowApiClient _api = new();
    private readonly FakeChatGateway _chat = new();

    private NotificationDispatcherService CreateService() =>
        new(_api, _chat, Options.Create(new BotOptions()), NullLogger<NotificationDispatcherService>.Instance);

    [Theory]
    [InlineData(NotificationKind.Invited, "Вас приглашают на созвон по вакансии «QA». Рекрутер свяжется с вами.")]
    [InlineData(NotificationKind.Rejected, "Спасибо за отклик на «QA». К сожалению, сейчас мы не готовы продолжить.")]
    public async Task RunOnce_SentNotificationIsMarkedSentWithCandidateText(NotificationKind kind, string expectedText)
    {
        var notification = new DueNotificationDto(Guid.NewGuid(), 777, kind, "QA");
        _api.Due.Add(notification);

        await CreateService().RunOnceAsync(CancellationToken.None);

        Assert.Equal((777L, expectedText), Assert.Single(_chat.Sent));
        Assert.Equal([notification.Id], _api.SentIds);
        Assert.Empty(_api.FailedIds);
    }

    [Fact]
    public async Task RunOnce_TelegramErrorIsMarkedFailedWithErrorAndNotSent()
    {
        var notification = new DueNotificationDto(Guid.NewGuid(), 777, NotificationKind.Invited, "QA");
        _api.Due.Add(notification);
        _chat.SendFailure = new InvalidOperationException("Forbidden: bot was blocked by the user");

        await CreateService().RunOnceAsync(CancellationToken.None);

        Assert.Empty(_api.SentIds);
        var failed = Assert.Single(_api.FailedIds);
        Assert.Equal(notification.Id, failed.Id);
        Assert.Equal("Forbidden: bot was blocked by the user", failed.Error);
    }
}
