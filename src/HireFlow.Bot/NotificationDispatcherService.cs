using HireFlow.Bot.Api;
using HireFlow.Bot.Dialog;
using HireFlow.Bot.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireFlow.Bot;

public class NotificationDispatcherService(
    IServiceScopeFactory scopes,
    IOptions<BotOptions> options,
    ILogger<NotificationDispatcherService> logger) : BackgroundService
{
    private const int ErrorMaxLength = 1000;

    // Раз в интервал забирает готовые уведомления и рассылает их; сбой итерации цикл не роняет.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Notification dispatcher started");
        using var timer = new PeriodicTimer(options.Value.NotificationPollInterval);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Notification iteration failed: {ErrorType}", ex.GetType().Name);
            }
        }
        while (await WaitNextAsync(timer, stoppingToken));
    }

    // Одна итерация в собственном scope: клиенты API и Telegram живут только на время итерации.
    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        await RunOnceAsync(
            scope.ServiceProvider.GetRequiredService<IHireFlowApiClient>(),
            scope.ServiceProvider.GetRequiredService<IChatGateway>(),
            ct);
    }

    // Получает уведомления, отправляет каждое и отмечает результат в API.
    private async Task RunOnceAsync(IHireFlowApiClient api, IChatGateway chat, CancellationToken ct)
    {
        var due = await api.GetDueNotificationsAsync(ct);
        if (!due.IsOk || due.Value is null)
        {
            logger.LogWarning("Could not load due notifications: {Status}", due.Status);
            return;
        }

        foreach (var notification in due.Value)
        {
            string? error = null;
            try
            {
                var text = BotTexts.Notification(notification.Type, notification.VacancyTitle);
                await chat.SendTextAsync(notification.TelegramId, text, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                error = ex.Message.Length > ErrorMaxLength ? ex.Message[..ErrorMaxLength] : ex.Message;
                if (string.IsNullOrWhiteSpace(error))
                    error = ex.GetType().Name;
            }

            var status = error is null
                ? await api.MarkNotificationSentAsync(notification.Id, ct)
                : await api.MarkNotificationFailedAsync(notification.Id, error, ct);

            logger.LogInformation(
                "Notification {NotificationId} {Outcome}, API status {Status}",
                notification.Id, error is null ? "sent" : "failed", status);
        }
    }

    // Ждёт следующий тик; false — остановка.
    private static async Task<bool> WaitNextAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
