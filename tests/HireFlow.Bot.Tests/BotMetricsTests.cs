using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Bot.Tests;

public class BotMetricsTests
{
    // Счётчики алертов стартуют с 0 сразу после создания: иначе ряд появляется уже со значением k, и increase не видит первую пачку.
    [Fact]
    public void Constructor_RecordsZeroForDroppedAndPollingErrors()
    {
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = services.GetRequiredService<IMeterFactory>();
        var measurements = new List<(string Instrument, long Value)>();
        // Слушатель включается до создания BotMetrics; свои измерения отличаем по фабрике, которая владеет meter.
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, meterFactory))
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            lock (measurements)
                measurements.Add((instrument.Name, value));
        });
        listener.Start();

        using var metrics = new BotMetrics(meterFactory).Meter;

        lock (measurements)
            Assert.Equal(
                [("hireflow.bot.messages.dropped", 0L), ("hireflow.bot.polling.errors", 0L)],
                measurements.OrderBy(m => m.Instrument));
    }
}
