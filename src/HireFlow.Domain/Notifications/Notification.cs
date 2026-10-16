namespace HireFlow.Domain.Notifications;

public class Notification
{
    public const int MaxAttempts = 5;
    public const int ErrorMaxLength = 1000;

    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationStatus Status { get; private set; }
    public DateTime SendAfter { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    // Нужен EF Core для создания объекта при чтении из БД.
    private Notification() { }

    // Создаёт уведомление в очереди. Вызывается только из JobApplication.
    internal Notification(NotificationType type, DateTime sendAfter)
    {
        Type = type;
        Status = NotificationStatus.Pending;
        SendAfter = sendAfter;
        CreatedAt = DateTime.UtcNow;
    }

    // Отмечает уведомление отправленным.
    public void MarkSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
    }

    // Записывает неудачную попытку: повтор через 2, 4, 8… минут, после MaxAttempts — ошибка.
    public void MarkFailed(string error)
    {
        if (Status != NotificationStatus.Pending)
            throw new DomainException("Only a pending notification can fail.");

        Attempts++;
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;

        if (Attempts >= MaxAttempts)
            Status = NotificationStatus.Failed;
        else
            SendAfter = DateTime.UtcNow.AddMinutes(Math.Pow(2, Attempts));
    }

    // Отменяет уведомление, если оно ещё не отправлено.
    internal void Cancel()
    {
        if (Status == NotificationStatus.Pending)
            Status = NotificationStatus.Cancelled;
    }
}
