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

    [Fact]
    public void Constructor_TruncatesFileNameTo255()
    {
        var application = NewApplication(new string('x', 400));

        Assert.Equal(JobApplication.FileNameMaxLength, application.ResumeFileName.Length);
    }

    [Fact]
    public void Constructor_KeepsFileNameOfExactlyMaxLength()
    {
        var name = new string('x', JobApplication.FileNameMaxLength);

        Assert.Equal(name, NewApplication(name).ResumeFileName);
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

    [Fact]
    public void Invite_WritesRecruiterHistoryEntryWithComment()
    {
        var application = ReviewApplication();

        application.Invite("Приглашаем");

        var entry = application.History[^1];
        Assert.Equal(ApplicationStatus.RecruiterReview, entry.FromStatus);
        Assert.Equal(ApplicationStatus.Invited, entry.ToStatus);
        Assert.Equal(StatusChangedBy.Recruiter, entry.ChangedBy);
        Assert.Equal("Приглашаем", entry.Reason);
    }

    [Fact]
    public void Invite_WithoutCommentLeavesReasonNull()
    {
        var application = ReviewApplication();

        application.Invite(null);

        Assert.Null(application.History[^1].Reason);
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

    [Fact]
    public void Invite_FromAutoRejectedDoesNotChangeAlreadySentRejection()
    {
        var application = AutoRejectedApplication();
        var rejection = application.Notifications[0];
        rejection.MarkSent();

        application.Invite(null);

        Assert.Equal(NotificationStatus.Sent, rejection.Status);
    }

    [Fact]
    public void Invite_FromReceivedThrows()
    {
        var application = NewApplication();

        Assert.Throws<DomainException>(() => application.Invite(null));

        Assert.Equal(ApplicationStatus.Received, application.Status);
        Assert.Empty(application.Notifications);
    }

    [Fact]
    public void Invite_FromInvitedThrowsAndDoesNotDuplicateNotification()
    {
        var application = ReviewApplication();
        application.Invite(null);

        Assert.Throws<DomainException>(() => application.Invite(null));

        Assert.Single(application.Notifications);
        Assert.Equal(2 + 1, application.History.Count);
    }

    [Fact]
    public void Invite_FromRejectedThrows()
    {
        var application = ReviewApplication();
        application.Reject(null, Delay);

        Assert.Throws<DomainException>(() => application.Invite(null));

        Assert.Equal(ApplicationStatus.Rejected, application.Status);
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

    [Fact]
    public void Reject_FromReceivedThrows()
    {
        var application = NewApplication();

        Assert.Throws<DomainException>(() => application.Reject(null, Delay));
    }

    [Fact]
    public void Reject_FromAutoRejectedThrows()
    {
        var application = AutoRejectedApplication();

        Assert.Throws<DomainException>(() => application.Reject(null, Delay));

        Assert.Equal(ApplicationStatus.AutoRejected, application.Status);
        Assert.Single(application.Notifications);
    }

    [Fact]
    public void Reject_FromInvitedThrows()
    {
        var application = ReviewApplication();
        application.Invite(null);

        Assert.Throws<DomainException>(() => application.Reject(null, Delay));
    }

    [Fact]
    public void Reject_FromRejectedThrowsAndDoesNotDuplicateNotification()
    {
        var application = ReviewApplication();
        application.Reject(null, Delay);

        Assert.Throws<DomainException>(() => application.Reject(null, Delay));

        Assert.Single(application.Notifications);
    }

    [Fact]
    public void StatusChange_UpdatesUpdatedAt()
    {
        var application = ReviewApplication();
        var previous = application.UpdatedAt;

        application.Invite(null);

        Assert.True(application.UpdatedAt >= previous);
    }

    [Fact]
    public void FullFlow_HistoryIsRecordedInOrder()
    {
        var application = AutoRejectedApplication();
        application.Invite(null);

        Assert.Equal(
            [
                (null, ApplicationStatus.Received),
                (ApplicationStatus.Received, ApplicationStatus.AutoRejected),
                (ApplicationStatus.AutoRejected, ApplicationStatus.Invited),
            ],
            application.History.Select(h => (h.FromStatus, h.ToStatus)).ToArray());
    }
}
