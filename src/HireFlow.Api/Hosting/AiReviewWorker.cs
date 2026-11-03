using HireFlow.Application.Ai;
using HireFlow.Infrastructure.Ai;
using Microsoft.Extensions.Options;

namespace HireFlow.Api.Hosting;

public class AiReviewWorker(IServiceScopeFactory scopes, IOptions<AiReviewOptions> options, ILogger<AiReviewWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // При выключенном ИИ не нагружаем базу сразу после запуска; старые задачи всё равно будут освобождены.
        if (!options.Value.Enabled)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<AiReviewProcessor>().ProcessOneAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("AI worker failed: {ErrorType}", ex.GetType().Name); }
            var delaySeconds = options.Value.Enabled ? Math.Clamp(options.Value.PollIntervalSeconds, 1, 300) : 30;
            try { await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
