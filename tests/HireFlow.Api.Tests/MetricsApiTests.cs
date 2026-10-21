using System.Net;
using HireFlow.Api.Hosting;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Common;
using HireFlow.Domain.JobApplications;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Доменные метрики API: счётчики пишутся при создании отклика и решении, gauges отражают БД после RefreshAsync.
public class MetricsApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    private HireFlowMetrics Metrics => App.Services.GetRequiredService<HireFlowMetrics>();

    [Theory]
    [InlineData(2, "passed")]
    [InlineData(1, "borderline")]
    [InlineData(0, "failed")]
    public async Task CreateApplication_CountsCreatedWithOnlyVerdictTag(int skillsInResume, string verdict)
    {
        using var capture = new MetricsCapture(Metrics.Meter);
        var skills = new[] { Unique("s"), Unique("s") };
        var vacancy = await CreateVacancyAsync(skills);
        var candidate = await CreateCandidateAsync();

        await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith([.. skills.Take(skillsInResume), "other"]));

        var sample = Assert.Single(capture.Of("hireflow.applications.created"));
        Assert.Equal(1, sample.Value);
        // Ключи тегов ровно {verdict}: ни id, ни имён, ни названия вакансии.
        Assert.Equal(["verdict"], sample.Tags.Keys);
        Assert.Equal(verdict, sample.Tags["verdict"]);
        Assert.Single(capture.Of("hireflow.screening.duration"));
    }

    [Theory]
    [InlineData(false, "invite", "recruiter_review")]
    [InlineData(false, "reject", "recruiter_review")]
    [InlineData(true, "invite", "auto_rejected")]
    public async Task Decide_CountsDecisionWithDecisionAndFromStatusTags(bool autoRejected, string decision, string fromStatus)
    {
        var application = autoRejected ? await CreateAutoRejectedApplicationAsync() : await CreateReviewApplicationAsync();
        using var capture = new MetricsCapture(Metrics.Meter);

        var response = await DecideAsync(application.Id, decision);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sample = Assert.Single(capture.Of("hireflow.applications.decisions"));
        Assert.Equal(1, sample.Value);
        Assert.Equal(["decision", "from_status"], sample.Tags.Keys.Order());
        Assert.Equal(decision, sample.Tags["decision"]);
        Assert.Equal(fromStatus, sample.Tags["from_status"]);
    }

    [Fact]
    public async Task RefreshAsync_GaugesFollowDatabase()
    {
        var snapshot = App.Services.GetRequiredService<MetricsSnapshotService>();
        using var capture = new MetricsCapture(Metrics.Meter);
        var before = await ObserveAsync(snapshot, capture);

        // Отказ скрининга: уведомление об отказе ждёт RejectionDelay (4 ч), поэтому пока не просрочено.
        var application = await CreateAutoRejectedApplicationAsync();
        var created = await ObserveAsync(snapshot, capture);
        Assert.Equal(before.ByStatus["auto_rejected"] + 1, created.ByStatus["auto_rejected"]);
        Assert.Equal(before.Overdue, created.Overdue);

        // Сдвиг заметно больше порога 5 мин, чтобы результат не зависел от времени между вызовами.
        var notification = Assert.Single(await GetNotificationsAsync(application.Id));
        await MakeDueAsync(notification.Id, TimeSpan.FromMinutes(10));
        var overdue = await ObserveAsync(snapshot, capture);
        Assert.Equal(before.Overdue + 1, overdue.Overdue);

        // Ряд на каждый статус, значения совпадают с БД.
        Assert.Equal(Enum.GetValues<ApplicationStatus>().Length, overdue.ByStatus.Count);
        foreach (var (status, count) in await CountByStatusAsync())
            Assert.Equal(count, overdue.ByStatus[HireFlowMetrics.ToTag(status)]);
    }

    // Обновляет снимок, запрашивает gauges и возвращает их значения.
    private static async Task<(Dictionary<string, double> ByStatus, double Overdue)> ObserveAsync(
        MetricsSnapshotService snapshot, MetricsCapture capture)
    {
        var skip = (capture.Of("hireflow.applications.by_status").Count, capture.Of("hireflow.notifications.overdue").Count);
        await snapshot.RefreshAsync(CancellationToken.None);
        capture.Observe();

        var byStatus = capture.Of("hireflow.applications.by_status").Skip(skip.Item1)
            .ToDictionary(s => (string)s.Tags["status"]!, s => s.Value);
        var overdue = Assert.Single(capture.Of("hireflow.notifications.overdue").Skip(skip.Item2));
        return (byStatus, overdue.Value);
    }

    // Считает отклики в БД по статусам.
    private async Task<Dictionary<ApplicationStatus, int>> CountByStatusAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.JobApplications.GroupBy(a => a.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }
}
