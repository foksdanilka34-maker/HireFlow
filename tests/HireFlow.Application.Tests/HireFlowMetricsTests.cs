using System.Diagnostics.Metrics;
using HireFlow.Application.Common;

namespace HireFlow.Application.Tests;

public class HireFlowMetricsTests
{
    // Фабрика meter без DI: создаёт обычный Meter, который видит MeterListener.
    private sealed class FakeMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);

        public void Dispose()
        {
        }
    }

    // Алерт ScreeningSlow считает долю откликов быстрее 60 с по корзине le="60": без неё порог SLO не посчитать.
    [Fact]
    public void ScreeningDuration_BucketBoundaries_ContainSloThreshold()
    {
        var metrics = new HireFlowMetrics(new FakeMeterFactory());
        using var meter = metrics.Meter;
        Instrument? screening = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter == metrics.Meter && instrument.Name == "hireflow.screening.duration")
            {
                screening = instrument;
            }
        };
        listener.Start();

        var histogram = Assert.IsType<Histogram<double>>(screening);
        var boundaries = Assert.IsAssignableFrom<IReadOnlyList<double>>(histogram.Advice?.HistogramBucketBoundaries);
        Assert.Contains(60d, boundaries);
    }
}
