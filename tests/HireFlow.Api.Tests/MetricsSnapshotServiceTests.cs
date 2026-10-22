using System.Diagnostics.Metrics;
using HireFlow.Api.Hosting;
using HireFlow.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HireFlow.Api.Tests;

// Gauges снимка без БД: свой meter и пустой провайдер, общий API-фикстуры и другие тесты не затрагиваются.
public class MetricsSnapshotServiceTests
{
    // Нули нового процесса дали бы ложный рост failed после рестарта API: до первого успешного снимка gauges молчат.
    [Fact]
    public async Task Gauges_BeforeFirstSuccessfulRefresh_ReportNothing()
    {
        using var meters = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var emptyServices = new ServiceCollection().BuildServiceProvider();
        var metrics = new HireFlowMetrics(meters.GetRequiredService<IMeterFactory>());
        using var meter = metrics.Meter;
        var service = new MetricsSnapshotService(
            emptyServices.GetRequiredService<IServiceScopeFactory>(), metrics, NullLogger<MetricsSnapshotService>.Instance);
        var published = new List<string>();
        var observed = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (!ReferenceEquals(instrument.Meter, meter))
                return;
            published.Add(instrument.Name);
            l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>((instrument, _, _, _) => observed.Add(instrument.Name));
        listener.Start();

        // Без обновления.
        listener.RecordObservableInstruments();
        Assert.Empty(observed);

        // Неудачное первое обновление (в провайдере нет IAppDbContext) снимок тоже не создаёт.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshAsync(CancellationToken.None));
        listener.RecordObservableInstruments();
        Assert.Empty(observed);

        // Инструменты действительно опубликованы, пустота не из-за опечатки в имени.
        Assert.Contains("hireflow.applications.by_status", published);
        Assert.Contains("hireflow.notifications.overdue", published);
        Assert.Contains("hireflow.notifications.failed", published);
    }
}
