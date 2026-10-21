using System.Diagnostics.Metrics;

namespace HireFlow.Api.Tests.Infrastructure;

// Записывает измерения инструментов одного экземпляра Meter: чужие meter с тем же именем (параллельные классы тестов) не попадают.
public sealed class MetricsCapture : IDisposable
{
    public sealed record Sample(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags);

    private readonly MeterListener _listener = new();
    private readonly List<Sample> _samples = [];

    // Начинает слушать инструменты указанного meter.
    public MetricsCapture(Meter meter)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, meter))
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((i, v, t, _) => Add(i, v, t));
        _listener.SetMeasurementEventCallback<int>((i, v, t, _) => Add(i, v, t));
        _listener.SetMeasurementEventCallback<double>((i, v, t, _) => Add(i, v, t));
        _listener.Start();
    }

    // Запрашивает текущие значения observable-инструментов (gauges).
    public void Observe() => _listener.RecordObservableInstruments();

    // Возвращает копию записанных измерений инструмента.
    public List<Sample> Of(string instrument)
    {
        lock (_samples)
            return _samples.Where(s => s.Instrument == instrument).ToList();
    }

    public void Dispose() => _listener.Dispose();

    private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var sample = new Sample(instrument.Name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value));
        lock (_samples)
            _samples.Add(sample);
    }
}
