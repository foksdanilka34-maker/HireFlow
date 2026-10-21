using HireFlow.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HireFlow.Api.Hosting;

// Готовность API: база отвечает на подключение.
public class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    // Проверяет подключение к базе; текст исключения в ответ не попадает.
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("database unavailable");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("database unavailable");
        }
    }
}
