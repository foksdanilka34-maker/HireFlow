using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using HireFlow.Domain.Screening;
using static HireFlow.Domain.Tests.TestData;

namespace HireFlow.Domain.Tests;

public class JobApplicationTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromHours(4);

    [Fact]
    public void Constructor_SetsReceivedStatusAndFirstHistoryEntry()
    {
        var application = NewApplication();

        Assert.Equal(ApplicationStatus.Received, application.Status);
        var entry = Assert.Single(application.History);
        Assert.Null(entry.FromStatus);
        Assert.Equal(ApplicationStatus.Received, entry.ToStatus);
        Assert.Equal(StatusChangedBy.System, entry.ChangedBy);
        Assert.Empty(application.Notifications);
    }

    [Theory]
    [InlineData(JobApplication.FileNameMaxLength)]
    [InlineData(JobApplication.FileNameMaxLength + 1)]
    public void Constructor_TruncatesFileNameToMaxLength(int length)
    {
        var application = NewApplication(new string('x', length));

        Assert.Equal(new string('x', JobApplication.FileNameMaxLength), application.ResumeFileName);
    }

    [Fact]
    public void ApplyScreening_FailedMovesToAutoRejectedAndQueuesDelayedRejection()
    {
        var application = NewApplication();
        var before = DateTime.UtcNow;

        application.ApplyScreening(ScreeningOf(ScreeningVerdict.Failed, 10), Delay);

        var after = DateTime.UtcNow;
        Assert.Equal(ApplicationStatus.AutoRejected, application.Status);
        var notification = Assert.Single(application.Notifications);
        Assert.Equal(NotificationType.Rejected, notification.Type);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.InRange(notification.SendAfter, before + Delay, after + Delay);
    }

    [Theory]
    [InlineData(ScreeningVerdict.Passed)]
    [InlineData(ScreeningVerdict.Borderline)]
    public void ApplyScreening_PassedOrBorderlineGoesToRecruiterReviewWithoutNotifications(ScreeningVerdict verdict)
    {
        var application = NewApplication();

        application.ApplyScreening(ScreeningOf(verdict, 75), Delay);

        Assert.Equal(ApplicationStatus.RecruiterReview, application.Status);
        Assert.Empty(application.Notifications);
    }

    [Fact]
    public void ApplyScreening_StoresResultAndWritesSystemHistoryEntry()
    {
        var application = NewApplication();
        var result = ScreeningOf(ScreeningVerdict.Failed, 10);

        application.ApplyScreening(result, Delay);

        Assert.Same(result, application.Screening);
        Assert.Equal(2, application.History.Count);
        var entry = application.History[1];
        Assert.Equal(ApplicationStatus.Received, entry.FromStatus);
        Assert.Equal(ApplicationStatus.AutoRejected, entry.ToStatus);
        Assert.Equal(StatusChangedBy.System, entry.ChangedBy);
        Assert.False(string.IsNullOrEmpty(entry.Reason));
    }

    [Theory]
    [InlineData(ScreeningVerdict.Passed)]
    [InlineData(ScreeningVerdict.Failed)]
    public void ApplyScreening_SecondCallThrows(ScreeningVerdict verdict)
    {
        var application = NewApplication();
        application.ApplyScreening(ScreeningOf(verdict), Delay);
        var status = application.Status;
        var historyCount = application.History.Count;

        Assert.Throws<DomainException>(() => application.ApplyScreening(ScreeningOf(ScreeningVerdict.Passed), Delay));

        Assert.Equal(status, application.Status);
        Assert.Equal(historyCount, application.History.Count);
    }

    [Fact]
    public void Invite_FromRecruiterReviewCreatesImmediateInvitedNotification()
    {
        var application = ReviewApplication();
        var before = DateTime.UtcNow;

        application.Invite("Приглашаем");

        var after = DateTime.UtcNow;
        Assert.Equal(ApplicationStatus.Invited, application.Status);
        var notification = Assert.Single(application.Notifications);
        Assert.Equal(NotificationType.Invited, notification.Type);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.InRange(notification.SendAfter, before, after);
    }

    [Theory]
    [InlineData("Приглашаем")]
    [InlineData(null)]
    public void Invite_WritesRecruiterHistoryEntryWithOptionalComment(string? comment)
    {
        var application = ReviewApplication();

        application.Invite(comment);

        var entry = application.History[^1];
        Assert.Equal(ApplicationStatus.RecruiterReview, entry.FromStatus);
        Assert.Equal(ApplicationStatus.Invited, entry.ToStatus);
        Assert.Equal(StatusChangedBy.Recruiter, entry.ChangedBy);
        Assert.Equal(comment, entry.Reason);
    }

    [Fact]
    public void Invite_FromAutoRejectedCancelsPendingRejectionAndQueuesInvitation()
    {
        var application = AutoRejectedApplication();
        var rejection = Assert.Single(application.Notifications);

        application.Invite("Ошибка системы");

        Assert.Equal(ApplicationStatus.Invited, application.Status);
        Assert.Equal(NotificationStatus.Cancelled, rejection.Status);
        Assert.Equal(2, application.Notifications.Count);
        var invitation = application.Notifications[1];
        Assert.Equal(NotificationType.Invited, invitation.Type);
        Assert.Equal(NotificationStatus.Pending, invitation.Status);
        Assert.True(invitation.SendAfter <= DateTime.UtcNow);
        Assert.Equal(ApplicationStatus.AutoRejected, application.History[^1].FromStatus);
        Assert.Equal(StatusChangedBy.Recruiter, application.History[^1].ChangedBy);
    }

    // Отказ уже отправлен или окончательно не доставлен: приглашение его не трогает.
    [Theory]
    [InlineData(NotificationStatus.Sent)]
    [InlineData(NotificationStatus.Failed)]
    public void Invite_FromAutoRejectedDoesNotChangeFinishedRejection(NotificationStatus finished)
    {
        var application = AutoRejectedApplication();
        var rejection = application.Notifications[0];
        if (finished == NotificationStatus.Sent)
            rejection.MarkSent();
        else
            for (var i = 0; i < Notification.MaxAttempts; i++)
                rejection.MarkFailed("boom");

        application.Invite(null);

        Assert.Equal(finished, rejection.Status);
    }

    // Отклик в нужном состоянии: «received», «auto_rejected», «invited», «rejected».
    private static JobApplication InState(string state)
    {
        switch (state)
        {
            case "received":
                return NewApplication();
            case "auto_rejected":
                return AutoRejectedApplication();
            case "invited":
                var invited = ReviewApplication();
                invited.Invite(null);
                return invited;
            default:
                var rejected = ReviewApplication();
                rejected.Reject(null, Delay);
                return rejected;
        }
    }

    [Theory]
    [InlineData("received")]
    [InlineData("invited")]
    [InlineData("rejected")]
    public void Invite_FromWrongStatusThrowsAndChangesNothing(string state)
    {
        var application = InState(state);
        var (status, notifications, history) = (application.Status, application.Notifications.Count, application.History.Count);

        Assert.Throws<DomainException>(() => application.Invite(null));

        Assert.Equal(status, application.Status);
        Assert.Equal(notifications, application.Notifications.Count);
        Assert.Equal(history, application.History.Count);
    }

    [Fact]
    public void Reject_FromRecruiterReviewQueuesDelayedRejection()
    {
        var application = ReviewApplication();
        var before = DateTime.UtcNow;

        application.Reject("Не подходит", Delay);

        var after = DateTime.UtcNow;
        Assert.Equal(ApplicationStatus.Rejected, application.Status);
        var notification = Assert.Single(application.Notifications);
        Assert.Equal(NotificationType.Rejected, notification.Type);
        Assert.InRange(notification.SendAfter, before + Delay, after + Delay);

        var entry = application.History[^1];
        Assert.Equal(ApplicationStatus.RecruiterReview, entry.FromStatus);
        Assert.Equal(ApplicationStatus.Rejected, entry.ToStatus);
        Assert.Equal(StatusChangedBy.Recruiter, entry.ChangedBy);
        Assert.Equal("Не подходит", entry.Reason);
    }

    [Theory]
    [InlineData("received")]
    [InlineData("auto_rejected")]
    [InlineData("invited")]
    [InlineData("rejected")]
    public void Reject_FromWrongStatusThrowsAndChangesNothing(string state)
    {
        var application = InState(state);
        var (status, notifications, history) = (application.Status, application.Notifications.Count, application.History.Count);

        Assert.Throws<DomainException>(() => application.Reject(null, Delay));

        Assert.Equal(status, application.Status);
        Assert.Equal(notifications, application.Notifications.Count);
        Assert.Equal(history, application.History.Count);
    }

    [Fact]
    public void StatusChange_UpdatesUpdatedAt()
    {
        var application = ReviewApplication();
        var before = DateTime.UtcNow;

        application.Invite(null);

        Assert.InRange(application.UpdatedAt, before, DateTime.UtcNow);
    }
}
