using HireFlow.Bot.Dialog;
using Microsoft.Extensions.Logging.Abstractions;

namespace HireFlow.Bot.Tests;

// Очереди по пользователям: порядок внутри одного пользователя, независимость разных, устойчивость к ошибкам и остановке.
public class UpdateDispatcherTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static IncomingMessage Message(long userId, string text) => new(userId, userId, text, null, null, null);

    private static UpdateDispatcher Create(Func<IncomingMessage, CancellationToken, Task> handler) =>
        new(4, handler, NullLogger<UpdateDispatcher>.Instance);

    [Fact]
    public async Task SameUserMessagesAreHandledInOrderAndNeverInParallel()
    {
        var handled = new List<string>();
        var running = 0;
        var maxRunning = 0;
        var dispatcher = Create(async (message, _) =>
        {
            maxRunning = Math.Max(maxRunning, Interlocked.Increment(ref running));
            await Task.Delay(10);
            handled.Add(message.Text!);
            Interlocked.Decrement(ref running);
        });
        dispatcher.Start();

        foreach (var text in new[] { "1", "2", "3", "4", "5" })
            await dispatcher.EnqueueAsync(Message(1, text), CancellationToken.None);
        await dispatcher.CompleteAsync().WaitAsync(Timeout);

        Assert.Equal(["1", "2", "3", "4", "5"], handled);
        Assert.Equal(1, maxRunning);
    }

    [Fact]
    public async Task StuckUserDoesNotDelayAnotherUser()
    {
        var release = new TaskCompletionSource();
        var otherHandled = new TaskCompletionSource();
        var dispatcher = Create(async (message, _) =>
        {
            if (message.UserId == 1)
                await release.Task;
            else
                otherHandled.SetResult();
        });
        dispatcher.Start();

        await dispatcher.EnqueueAsync(Message(1, "stuck"), CancellationToken.None);
        await dispatcher.EnqueueAsync(Message(2, "other"), CancellationToken.None);

        await otherHandled.Task.WaitAsync(Timeout);
        release.SetResult();
        await dispatcher.CompleteAsync().WaitAsync(Timeout);
    }

    [Fact]
    public async Task HandlerExceptionDoesNotStopTheQueue()
    {
        var handled = new List<string>();
        var dispatcher = Create((message, _) =>
        {
            if (message.Text == "bad")
                throw new InvalidOperationException("boom");
            handled.Add(message.Text!);
            return Task.CompletedTask;
        });
        dispatcher.Start();

        await dispatcher.EnqueueAsync(Message(1, "bad"), CancellationToken.None);
        await dispatcher.EnqueueAsync(Message(1, "next"), CancellationToken.None);
        await dispatcher.CompleteAsync().WaitAsync(Timeout);

        Assert.Equal(["next"], handled);
    }

    [Fact]
    public async Task OnStopAlreadyReceivedMessagesAreStillHandled()
    {
        var release = new TaskCompletionSource();
        var handled = new List<string>();
        var dispatcher = Create(async (message, _) =>
        {
            await release.Task;
            handled.Add(message.Text!);
        });
        dispatcher.Start();
        foreach (var text in new[] { "1", "2", "3" })
            await dispatcher.EnqueueAsync(Message(1, text), CancellationToken.None);

        var completing = dispatcher.CompleteAsync();
        release.SetResult();
        await completing.WaitAsync(Timeout);

        Assert.Equal(["1", "2", "3"], handled);
    }
}
