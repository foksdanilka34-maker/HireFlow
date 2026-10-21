using System.Diagnostics.Metrics;
using HireFlow.Application.Common;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Api.Hosting;

// Раз в 30 с считает снимок из БД для observable gauges; колбэки экспорта читают только поля снимка.
public class MetricsSnapshotService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OverdueAfter = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MetricsSnapshotService> _logger;
    private volatile Snapshot _snapshot = new(new Dictionary<ApplicationStatus, int>(), 0, 0);

    // Регистрирует gauges на meter HireFlow.
    public MetricsSnapshotService(IServiceScopeFactory scopes, HireFlowMetrics metrics, ILogger<MetricsSnapshotService> logger)
    {
        _scopes = scopes;
        _logger = logger;
        metrics.Meter.CreateObservableGauge("hireflow.applications.by_status", ObserveByStatus, description: "Отклики по статусам");
        metrics.Meter.CreateObservableGauge("hireflow.notifications.overdue", () => _snapshot.Overdue,
            description: "Уведомления pending, просроченные больше чем на 5 минут");
        metrics.Meter.CreateObservableGauge("hireflow.notifications.failed", () => _snapshot.Failed,
            description: "Уведомления в статусе failed");
    }

    // Обновляет снимок по таймеру; сбой обновления пишет в лог тип исключения и оставляет прежний снимок.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        do
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Metrics snapshot refresh failed: {ErrorType}", ex.GetType().Name);
            }
        }
        while (await WaitNextAsync(timer, stoppingToken));
    }

    // Считает число откликов по статусам и проблемные уведомления и заменяет снимок.
    public async Task RefreshAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var counts = await db.JobApplications
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        var byStatus = Enum.GetValues<ApplicationStatus>().ToDictionary(s => s, s => counts.GetValueOrDefault(s));

        var overdueBefore = DateTime.UtcNow - OverdueAfter;
        var overdue = await db.Notifications
            .CountAsync(n => n.Status == NotificationStatus.Pending && n.SendAfter < overdueBefore, ct);
        var failed = await db.Notifications.CountAsync(n => n.Status == NotificationStatus.Failed, ct);

        _snapshot = new Snapshot(byStatus, overdue, failed);
    }

    // Отдаёт число откликов по каждому статусу из снимка.
    private IEnumerable<Measurement<int>> ObserveByStatus() =>
        _snapshot.ByStatus.Select(p =>
            new Measurement<int>(p.Value, new KeyValuePair<string, object?>("status", HireFlowMetrics.ToTag(p.Key))));

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

    private sealed record Snapshot(IReadOnlyDictionary<ApplicationStatus, int> ByStatus, int Overdue, int Failed);
}
