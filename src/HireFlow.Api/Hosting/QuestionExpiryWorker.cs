using HireFlow.Application.JobApplications;

namespace HireFlow.Api.Hosting;

public class QuestionExpiryWorker(IServiceScopeFactory scopes, ILogger<QuestionExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<QuestionnaireService>().ExpireOverdueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Question expiration failed: {ErrorType}", ex.GetType().Name); }
        }
    }
}
