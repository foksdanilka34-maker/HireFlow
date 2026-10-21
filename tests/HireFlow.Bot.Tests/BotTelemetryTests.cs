using System.Diagnostics;
using HireFlow.Bot.Dialog;
using Microsoft.Extensions.Logging.Abstractions;

namespace HireFlow.Bot.Tests;

// Трассировка бота: токен Telegram не уходит в трейсы, спан сообщения не содержит персональных данных.
public class BotTelemetryTests
{
    [Theory]
    [InlineData("https://api.telegram.org/bot123456:ABCdefGHIjklMNO/getUpdates", false)]
    [InlineData("http://api:8080/candidates/1", true)]
    public void ShouldTrace_TelegramHostIsNotTraced(string url, bool expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        Assert.Equal(expected, BotTelemetry.ShouldTrace(request));
    }

    [Fact]
    public async Task HandledMessage_CreatesSpanWithOnlyKindTag_WithoutTelegramId()
    {
        const long telegramId = 987654321;
        var finished = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BotTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (finished) finished.Add(activity); },
        };
        ActivitySource.AddActivityListener(listener);
        // Другие тесты процесса тоже создают спаны бота: свой отличаем по trace id, который виден в обработчике.
        ActivityTraceId? traceId = null;
        var dispatcher = new UpdateDispatcher(
            2,
            (_, _) =>
            {
                traceId = Activity.Current?.TraceId;
                return Task.CompletedTask;
            },
            NullLogger<UpdateDispatcher>.Instance);
        dispatcher.Start();

        await dispatcher.EnqueueAsync(
            new IncomingMessage(telegramId, telegramId, "Иван, +79991234567", null, "Иван", "Петров"), CancellationToken.None);
        await dispatcher.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(traceId);
        Activity span;
        lock (finished)
            span = Assert.Single(finished, a => a.TraceId == traceId);
        Assert.Equal("bot.message", span.OperationName);
        Assert.Equal([BotTelemetry.MessageKindTag], span.TagObjects.Select(t => t.Key));
        Assert.Equal("text", span.GetTagItem(BotTelemetry.MessageKindTag));
        Assert.DoesNotContain(span.TagObjects, t => t.Value?.ToString()?.Contains(telegramId.ToString()) == true);
    }
}
