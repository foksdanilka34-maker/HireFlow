using System.ComponentModel.DataAnnotations;
using HireFlow.Domain.Notifications;

namespace HireFlow.Application.Notifications;

public record DueNotificationResponse(Guid Id, long TelegramId, NotificationType Type, string VacancyTitle);

public record NotificationFailedRequest
{
    [Required, MaxLength(Notification.ErrorMaxLength)]
    public string Error { get; init; } = "";
}
