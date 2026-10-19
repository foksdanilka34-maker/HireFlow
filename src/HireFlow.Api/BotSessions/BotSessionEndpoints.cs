using HireFlow.Api.Security;
using HireFlow.Application.BotSessions;

namespace HireFlow.Api.BotSessions;

public static class BotSessionEndpoints
{
    // Регистрирует служебные маршруты /bot-sessions для бота.
    public static void MapBotSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bot-sessions").RequireAuthorization(AuthPolicies.Bot);

        group.MapGet("/{telegramId:long:min(1)}", (long telegramId, IBotSessionService service, CancellationToken ct) =>
            service.GetAsync(telegramId, ct));

        group.MapPut("/{telegramId:long:min(1)}",
            (long telegramId, BotSessionRequest request, IBotSessionService service, CancellationToken ct) =>
                service.UpsertAsync(telegramId, request, ct));
    }
}
