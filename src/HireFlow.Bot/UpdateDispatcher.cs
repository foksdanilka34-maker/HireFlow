using System.Threading.Channels;
using HireFlow.Bot.Dialog;
using Microsoft.Extensions.Logging;

namespace HireFlow.Bot;

// Раскладывает сообщения по N очередям по userId: один кандидат — строго по порядку, разные — параллельно.
public class UpdateDispatcher(
    int workers,
    Func<IncomingMessage, CancellationToken, Task> handler,
    ILogger<UpdateDispatcher> logger)
{
    private const int QueueCapacity = 100;

    private readonly Channel<IncomingMessage>[] _queues = Enumerable.Range(0, workers)
        .Select(_ => Channel.CreateBounded<IncomingMessage>(
            new BoundedChannelOptions(QueueCapacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true }))
        .ToArray();

    private Task[] _workers = [];

    // Запускает по одной рабочей задаче на очередь; отмены обработки нет намеренно — при остановке очереди дообрабатываются.
    public void Start() =>
        _workers = _queues.Select(q => Task.Run(() => RunWorkerAsync(q.Reader))).ToArray();

    // Ставит сообщение в очередь его пользователя; очередь заполнена — сообщение отбрасывается, опрос Telegram не ждёт.
    public ValueTask EnqueueAsync(IncomingMessage message, CancellationToken ct)
    {
        var index = (int)((ulong)message.UserId % (ulong)_queues.Length);
        if (!_queues[index].Writer.TryWrite(message))
            logger.LogWarning("Message dropped: queue {QueueIndex} is full", index);
        return ValueTask.CompletedTask;
    }

    // Закрывает очереди и ждёт, пока рабочие задачи дообработают уже полученные сообщения.
    public Task CompleteAsync()
    {
        foreach (var queue in _queues)
            queue.Writer.TryComplete();
        return Task.WhenAll(_workers);
    }

    // Обрабатывает сообщения одной очереди по порядку; исключение одного сообщения не останавливает очередь.
    private async Task RunWorkerAsync(ChannelReader<IncomingMessage> reader)
    {
        await foreach (var message in reader.ReadAllAsync(CancellationToken.None))
        {
            try
            {
                await handler(message, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError("Message handling failed: {ErrorType}", ex.GetType().Name);
            }
        }
    }
}
