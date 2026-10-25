using System.Diagnostics;
using HireFlow.Api.Security;
using HireFlow.Application.Common;

namespace HireFlow.Api.PublicApi;

// Метрика и строка журнала на запрос к /v1: только клиент, маршрут, код, размерный класс и длительность.
// Подключается до UseExceptionHandler, чтобы видеть итоговый код, в том числе 401 и 429.
public class PublicApiMetricsMiddleware(RequestDelegate next, HireFlowMetrics metrics, ILogger<PublicApiMetricsMiddleware> logger)
{
    // Граница размерного класса: тело до 2 МБ (или без Content-Length) — small, больше — large.
    private const long SmallBodyMaxSize = 2 * 1024 * 1024;

    // Пропускает запросы вне /v1 и документацию, для остальных замеряет длительность и пишет метрику и лог.
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (!path.StartsWithSegments("/v1") || path.StartsWithSegments("/v1/docs")
            || path.StartsWithSegments("/v1/openapi.json") || route is null)
        {
            await next(context);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        var statusCode = StatusCodes.Status500InternalServerError;
        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(start);
            // Имя берётся только из ключа публичного API: cookie рекрутера или ключ бота клиентом не считаются.
            var client = context.User.Identities
                .FirstOrDefault(i => i.AuthenticationType == PublicApiKeyHandler.SchemeName)?.Name ?? "anonymous";
            var size = context.Request.ContentLength > SmallBodyMaxSize ? "large" : "small";
            metrics.PublicApiRequest(client, route, statusCode, size, elapsed);
            logger.LogInformation("Public API request: client {Client}, route {Route}, status {StatusCode}, {ElapsedMs} ms",
                client, route, statusCode, (long)elapsed.TotalMilliseconds);
        }
    }
}
