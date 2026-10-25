using HireFlow.Bot.Dialog;
using global::Telegram.Bot;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HireFlow.Bot;

public class UpdateReceiverService(
    IServiceScopeFactory scopes,
    UpdateDispatcher dispatcher,
    ILogger<UpdateReceiverService> logger,
    BotMetrics metrics) : BackgroundService
{
    private const int LongPollSeconds = 30;
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);

    // Читает обновления Telegram long polling'ом и передаёт сообщения в очереди; при остановке дожидается их обработки.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Update receiver started");
        var offset = 0;
        dispatcher.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Update[] updates;
                using (var pollScope = scopes.CreateScope())
                {
                    var client = pollScope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
                    updates = await client.GetUpdates(
                        offset, timeout: LongPollSeconds, allowedUpdates: [UpdateType.Message], cancellationToken: stoppingToken);
                }

                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    if (Map(update) is { } message)
                        await dispatcher.EnqueueAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Polling failed: {ErrorType}", ex.GetType().Name);
                metrics.PollingError();
                await DelayAsync(stoppingToken);
            }
        }

        await dispatcher.CompleteAsync();
        logger.LogInformation("Update receiver stopped");
    }

    // Превращает обновление Telegram в сообщение диалога; не личный чат или не сообщение — null.
    private static IncomingMessage? Map(Update update)
    {
        var message = update.Message;
        if (message is null || message.From is null || message.Chat.Type != ChatType.Private)
            return null;

        var document = message.Document is { } d ? new IncomingDocument(d.FileId, d.FileName, d.FileSize) : null;
        return new IncomingMessage(
            message.From.Id, message.Chat.Id, message.Text, document, message.From.FirstName, message.From.LastName);
    }

    // Пауза после сбоя, не падающая при остановке.
    private static async Task DelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(ErrorDelay, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
