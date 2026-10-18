using HireFlow.Bot.Dialog;
using global::Telegram.Bot;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HireFlow.Bot;

public class UpdateReceiverService(
    ITelegramBotClient client,
    DialogHandler dialog,
    ILogger<UpdateReceiverService> logger) : BackgroundService
{
    private const int LongPollSeconds = 30;
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);

    // Читает обновления Telegram long polling'ом и обрабатывает сообщения строго по одному.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Update receiver started");
        var offset = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await client.GetUpdates(
                    offset, timeout: LongPollSeconds, allowedUpdates: [UpdateType.Message], cancellationToken: stoppingToken);

                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    await ProcessAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Polling failed: {ErrorType}", ex.GetType().Name);
                await DelayAsync(stoppingToken);
            }
        }

        logger.LogInformation("Update receiver stopped");
    }

    // Обрабатывает одно обновление; исключение не прерывает цикл.
    private async Task ProcessAsync(Update update, CancellationToken ct)
    {
        try
        {
            var message = Map(update);
            if (message is not null)
                await dialog.HandleAsync(message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError("Message handling failed: {ErrorType}", ex.GetType().Name);
        }
    }

    // Превращает обновление Telegram в сообщение диалога; не личный чат или не сообщение — null.
    internal static IncomingMessage? Map(Update update)
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
