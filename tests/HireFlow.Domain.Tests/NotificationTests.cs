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
        application.Invite(RecruiterId, null);
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

    // Уведомление в завершённом состоянии: «sent», «cancelled» или «failed».
    private static Notification FinishedNotification(string state)
    {
        if (state == "cancelled")
            return CancelledNotification();
        if (state == "failed")
            return FailedNotification();

        var sent = PendingNotification();
        sent.MarkSent();
        return sent;
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

    [Theory]
    [InlineData(Notification.ErrorMaxLength + 500)]
    [InlineData(Notification.ErrorMaxLength)]
    public void MarkFailed_TruncatesErrorToMaxLength(int length)
    {
        var notification = PendingNotification();

        notification.MarkFailed(new string('e', length));

        Assert.Equal(new string('e', Notification.ErrorMaxLength), notification.LastError);
    }

    [Theory]
    [InlineData("sent")]
    [InlineData("cancelled")]
    [InlineData("failed")]
    public void MarkFailed_ThrowsForFinishedNotificationAndChangesNothing(string state)
    {
        var notification = FinishedNotification(state);
        var (status, attempts) = (notification.Status, notification.Attempts);

        Assert.Throws<DomainException>(() => notification.MarkFailed("boom"));

        Assert.Equal(status, notification.Status);
        Assert.Equal(attempts, notification.Attempts);
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

    [Theory]
    [InlineData("cancelled")]
    [InlineData("failed")]
    public void MarkSent_ThrowsForCancelledOrFailedNotification(string state)
    {
        var notification = FinishedNotification(state);
        var status = notification.Status;

        Assert.Throws<DomainException>(() => notification.MarkSent());

        Assert.Equal(status, notification.Status);
        Assert.Null(notification.SentAt);
    }
}
