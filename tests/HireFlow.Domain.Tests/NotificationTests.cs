using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using static HireFlow.Domain.Tests.TestData;

namespace HireFlow.Domain.Tests;

public class NotificationTests
{
    // Создаёт ожидающее уведомление об отказе через автоотказ.
    private static Notification PendingNotification() => AutoRejectedApplication().Notifications.Single();

    // Создаёт отменённое уведомление: приглашение из автоотказа отменяет отказ.
    private static Notification CancelledNotification()
    {
        var application = AutoRejectedApplication();
        var rejection = application.Notifications.Single();
        application.Invite(null);
        return rejection;
    }

    // Создаёт уведомление в статусе «ошибка» после пяти неудач.
    private static Notification FailedNotification()
    {
        var notification = PendingNotification();
        for (var i = 0; i < Notification.MaxAttempts; i++)
            notification.MarkFailed("boom");
        return notification;
    }

    [Fact]
    public void NewNotification_IsPendingWithZeroAttempts()
    {
        var notification = PendingNotification();

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(0, notification.Attempts);
        Assert.Null(notification.SentAt);
        Assert.Null(notification.LastError);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void MarkFailed_PostponesSendAfterByPowersOfTwoMinutes(int failures, int expectedMinutes)
    {
        var notification = PendingNotification();
        for (var i = 1; i < failures; i++)
            notification.MarkFailed("boom");

        var before = DateTime.UtcNow;
        notification.MarkFailed("boom");
        var after = DateTime.UtcNow;

        Assert.Equal(failures, notification.Attempts);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.InRange(
            notification.SendAfter,
            before.AddMinutes(expectedMinutes),
            after.AddMinutes(expectedMinutes));
    }

    [Fact]
    public void MarkFailed_FifthFailureMakesNotificationFailed()
    {
        var notification = FailedNotification();

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal(Notification.MaxAttempts, notification.Attempts);
    }

    [Fact]
    public void MarkFailed_RecordsLastError()
    {
        var notification = PendingNotification();

        notification.MarkFailed("chat not found");

        Assert.Equal("chat not found", notification.LastError);
    }

    [Fact]
    public void MarkFailed_TruncatesErrorTo1000()
    {
        var notification = PendingNotification();

        notification.MarkFailed(new string('e', 1500));

        Assert.Equal(Notification.ErrorMaxLength, notification.LastError!.Length);
    }

    [Fact]
    public void MarkFailed_KeepsErrorOfExactlyMaxLength()
    {
        var notification = PendingNotification();
        var error = new string('e', Notification.ErrorMaxLength);

        notification.MarkFailed(error);

        Assert.Equal(error, notification.LastError);
    }

    [Fact]
    public void MarkFailed_ThrowsForSentNotification()
    {
        var notification = PendingNotification();
        notification.MarkSent();

        Assert.Throws<DomainException>(() => notification.MarkFailed("boom"));

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(0, notification.Attempts);
    }

    [Fact]
    public void MarkFailed_ThrowsForCancelledNotification()
    {
        var notification = CancelledNotification();

        Assert.Throws<DomainException>(() => notification.MarkFailed("boom"));

        Assert.Equal(NotificationStatus.Cancelled, notification.Status);
    }

    [Fact]
    public void MarkFailed_ThrowsForAlreadyFailedNotification()
    {
        var notification = FailedNotification();

        Assert.Throws<DomainException>(() => notification.MarkFailed("boom"));

        Assert.Equal(Notification.MaxAttempts, notification.Attempts);
    }

    [Fact]
    public void MarkSent_PendingBecomesSentWithTimestamp()
    {
        var notification = PendingNotification();
        var before = DateTime.UtcNow;

        notification.MarkSent();

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.InRange(notification.SentAt!.Value, before, DateTime.UtcNow);
    }

    [Fact]
    public void MarkSent_SentStaysSentWithoutErrorAndKeepsTimestamp()
    {
        var notification = PendingNotification();
        notification.MarkSent();
        var sentAt = notification.SentAt;

        notification.MarkSent();

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(sentAt, notification.SentAt);
    }

    [Fact]
    public void MarkSent_ThrowsForCancelledNotification()
    {
        var notification = CancelledNotification();

        Assert.Throws<DomainException>(() => notification.MarkSent());

        Assert.Equal(NotificationStatus.Cancelled, notification.Status);
        Assert.Null(notification.SentAt);
    }

    [Fact]
    public void MarkSent_ThrowsForFailedNotification()
    {
        var notification = FailedNotification();

        Assert.Throws<DomainException>(() => notification.MarkSent());

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Null(notification.SentAt);
    }

    [Fact]
    public void Invite_DoesNotCancelFailedRejection()
    {
        var application = AutoRejectedApplication();
        var rejection = application.Notifications.Single();
        for (var i = 0; i < Notification.MaxAttempts; i++)
            rejection.MarkFailed("boom");

        application.Invite(null);

        Assert.Equal(NotificationStatus.Failed, rejection.Status);
    }
}
