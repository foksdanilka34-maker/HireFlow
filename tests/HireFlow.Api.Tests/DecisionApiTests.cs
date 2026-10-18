using System.Net;
using System.Net.Http.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.JobApplications;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;

namespace HireFlow.Api.Tests;

public class DecisionApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    [Fact]
    public async Task Invite_MovesToInvitedAndNotificationIsImmediatelyDue()
    {
        var created = await CreateReviewApplicationAsync();

        var response = await DecideAsync(created.Id, "invite", "Ждём вас на созвон");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var application = await ReadAsync<ApplicationResponse>(response);
        Assert.Equal(ApplicationStatus.Invited, application.Status);
        var last = application.History[^1];
        Assert.Equal(ApplicationStatus.RecruiterReview, last.FromStatus);
        Assert.Equal(ApplicationStatus.Invited, last.ToStatus);
        Assert.Equal(StatusChangedBy.Recruiter, last.ChangedBy);
        Assert.Equal("Ждём вас на созвон", last.Reason);

        var notification = Assert.Single(await GetNotificationsAsync(created.Id));
        Assert.Equal(NotificationType.Invited, notification.Type);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Contains(await GetDueAsync(), d => d.Id == notification.Id && d.Type == NotificationType.Invited);
    }

    [Fact]
    public async Task Invite_PersistsStatusInDatabase()
    {
        var created = await CreateReviewApplicationAsync();
        await DecideAsync(created.Id, "invite");

        var loaded = await ReadAsync<ApplicationResponse>(await Client.GetAsync($"/applications/{created.Id}"));

        Assert.Equal(ApplicationStatus.Invited, loaded.Status);
        Assert.Equal(3, loaded.History.Count);
    }

    [Fact]
    public async Task Reject_MovesToRejectedAndQueuesDelayedNotification()
    {
        var created = await CreateReviewApplicationAsync();
        var before = DateTime.UtcNow;

        var response = await DecideAsync(created.Id, "reject", "Не подходит по опыту");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var application = await ReadAsync<ApplicationResponse>(response);
        Assert.Equal(ApplicationStatus.Rejected, application.Status);
        Assert.Equal("Не подходит по опыту", application.History[^1].Reason);
        Assert.Equal(StatusChangedBy.Recruiter, application.History[^1].ChangedBy);

        var notification = Assert.Single(await GetNotificationsAsync(created.Id));
        Assert.Equal(NotificationType.Rejected, notification.Type);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.InRange(notification.SendAfter, before.AddHours(4), DateTime.UtcNow.AddHours(4));
        Assert.DoesNotContain(await GetDueAsync(), d => d.Id == notification.Id);
    }

    [Fact]
    public async Task Reject_FromAutoRejectedReturns409AndChangesNothing()
    {
        var created = await CreateAutoRejectedApplicationAsync();

        var response = await DecideAsync(created.Id, "reject");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var loaded = await ReadAsync<ApplicationResponse>(await Client.GetAsync($"/applications/{created.Id}"));
        Assert.Equal(ApplicationStatus.AutoRejected, loaded.Status);
        Assert.Single(await GetNotificationsAsync(created.Id));
    }

    [Fact]
    public async Task Invite_FromAutoRejectedCancelsPendingRejection()
    {
        var created = await CreateAutoRejectedApplicationAsync();
        var rejection = Assert.Single(await GetNotificationsAsync(created.Id));
        await MakeDueAsync(rejection.Id);
        Assert.Contains(await GetDueAsync(), d => d.Id == rejection.Id);

        var response = await DecideAsync(created.Id, "invite", "Система ошиблась");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ApplicationStatus.Invited, (await ReadAsync<ApplicationResponse>(response)).Status);
        var notifications = await GetNotificationsAsync(created.Id);
        Assert.Equal(2, notifications.Count);
        var cancelled = notifications.Single(n => n.Type == NotificationType.Rejected);
        var invitation = notifications.Single(n => n.Type == NotificationType.Invited);
        Assert.Equal(rejection.Id, cancelled.Id);
        Assert.Equal(NotificationStatus.Cancelled, cancelled.Status);
        Assert.Equal(NotificationStatus.Pending, invitation.Status);

        var due = await GetDueAsync();
        Assert.DoesNotContain(due, d => d.Id == cancelled.Id);
        Assert.Contains(due, d => d.Id == invitation.Id);
    }

    [Fact]
    public async Task Decision_SecondDecisionReturns409()
    {
        var created = await CreateReviewApplicationAsync();
        (await DecideAsync(created.Id, "invite")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Conflict, (await DecideAsync(created.Id, "invite")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await DecideAsync(created.Id, "reject")).StatusCode);
        Assert.Single(await GetNotificationsAsync(created.Id));
    }

    [Fact]
    public async Task Decision_RejectedCannotBeInvited()
    {
        var created = await CreateReviewApplicationAsync();
        (await DecideAsync(created.Id, "reject")).EnsureSuccessStatusCode();

        var response = await DecideAsync(created.Id, "invite");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Decision_WithoutDecisionReturns400()
    {
        var created = await CreateReviewApplicationAsync();

        var response = await DecideAsync(created.Id, decision: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var loaded = await ReadAsync<ApplicationResponse>(await Client.GetAsync($"/applications/{created.Id}"));
        Assert.Equal(ApplicationStatus.RecruiterReview, loaded.Status);
    }

    [Fact]
    public async Task Decision_UnknownDecisionValueReturns400()
    {
        var created = await CreateReviewApplicationAsync();

        var response = await DecideAsync(created.Id, "bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Decision_TooLongCommentReturns400()
    {
        var created = await CreateReviewApplicationAsync();

        var response = await DecideAsync(created.Id, "invite", new string('x', 1001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Decision_UnknownApplicationReturns404()
    {
        var response = await DecideAsync(Guid.NewGuid(), "invite");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
