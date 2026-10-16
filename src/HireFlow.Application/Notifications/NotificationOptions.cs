namespace HireFlow.Application.Notifications;

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    public TimeSpan RejectionDelay { get; init; } = TimeSpan.FromHours(4);
}
