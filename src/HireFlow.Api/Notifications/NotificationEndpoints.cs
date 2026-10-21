using HireFlow.Api.Security;
using HireFlow.Application.Notifications;

namespace HireFlow.Api.Notifications;

public static class NotificationEndpoints
{
    // Регистрирует служебные маршруты /internal/notifications для бота.
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/notifications").RequireAuthorization(AuthPolicies.Bot);

        group.MapGet("/due", (INotificationService service, CancellationToken ct) =>
            service.GetDueAsync(ct));

        group.MapPost("/{id:guid}/sent", async (Guid id, INotificationService service, CancellationToken ct) =>
        {
            await service.MarkSentAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/failed",
            async (Guid id, NotificationFailedRequest request, INotificationService service, CancellationToken ct) =>
            {
                await service.MarkFailedAsync(id, request, ct);
                return Results.NoContent();
            });
    }
}
