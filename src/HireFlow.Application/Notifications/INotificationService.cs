namespace HireFlow.Application.Notifications;

public interface INotificationService
{
    // Возвращает уведомления, которым пора уйти кандидатам, самые старые первыми.
    Task<List<DueNotificationResponse>> GetDueAsync(CancellationToken ct);

    // Отмечает уведомление отправленным.
    Task MarkSentAsync(Guid id, CancellationToken ct);

    // Записывает неудачную отправку: уведомление повторится позже или станет ошибочным.
    Task MarkFailedAsync(Guid id, NotificationFailedRequest request, CancellationToken ct);
}
