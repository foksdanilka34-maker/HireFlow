using System.Net;
using System.Net.Http.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Ai;
using HireFlow.Application.Common;
using HireFlow.Domain.Ai;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Vacancies;
using HireFlow.Infrastructure.Ai;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireFlow.Api.Tests;

public class AiReviewFixture : ApiAppFixture
{
    public FakeReviewer Reviewer { get; } = new();
    public FakeVacancyAssistant VacancyAssistant { get; } = new();
    protected override IReadOnlyDictionary<string, string> ExtraSettings => new Dictionary<string, string>
    {
        ["AiReview:Enabled"] = "true", ["AiReview:MaxAttempts"] = "2", ["AiReview:RetryDelaySeconds"] = "1"
    };
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IAiReviewer>();
        services.AddSingleton<IAiReviewer>(Reviewer);
        services.RemoveAll<IVacancyAiAssistant>();
        services.AddSingleton<IVacancyAiAssistant>(VacancyAssistant);
    }
}

public class FakeVacancyAssistant : IVacancyAiAssistant
{
    public bool Fail { get; set; }
    public Task<VacancyAiResult> SuggestAsync(VacancyAiRequest request, CancellationToken ct) => Fail
        ? Task.FromException<VacancyAiResult>(new HttpRequestException("unavailable"))
        : Task.FromResult(new VacancyAiResult("Предлагаемое описание", ["Уточните график"], [new VacancyQuestion { Text = "Какой график вам подходит?" }]));
}

public class FakeReviewer : IAiReviewer
{
    public bool Fail { get; set; }
    public string Recommendation { get; set; } = "review";
    public TaskCompletionSource? Started { get; set; }
    public TaskCompletionSource? Release { get; set; }
    public VacancyRequirements? LastRequirements { get; private set; }
    public async Task<AiReviewResult> ReviewAsync(JobApplication application, VacancyRequirements requirements, string? candidateName, string? phone, CancellationToken ct)
    {
        LastRequirements = requirements;
        Started?.TrySetResult();
        if (Release is not null) await Release.Task.WaitAsync(ct);
        if (Fail) throw new HttpRequestException("unavailable");
        return new AiReviewResult(Recommendation, 60, "Требуется проверка опыта.", "test-model", "v1", DateTime.UtcNow);
    }
    public Task<AiReviewResult> ReviewAnswersAsync(JobApplication application, string? candidateName, string? phone, CancellationToken ct) =>
        Fail ? Task.FromException<AiReviewResult>(new HttpRequestException("unavailable")) :
        Task.FromResult(new AiReviewResult("match", 100, "Ответы соответствуют.", "test-model", "answers-v1", DateTime.UtcNow));

}

public class AiReviewTests(AiReviewFixture fixture) : ApiTestBase(fixture), IClassFixture<AiReviewFixture>
{
    [Fact]
    public async Task VacancySuggestionsRequireRecruiterAndDoNotCreateVacancy()
    {
        var request = new VacancyAiRequest { Title = Unique("Vacancy "), RequiredSkills = ["C#"] };
        using var botOnly = fixture.CreateClient(ApiFactory.BotApiKey);
        var denied = await botOnly.PostAsJsonAsync("/vacancies/ai-suggestions", request, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var before = await ReadAsync<List<HireFlow.Application.Vacancies.VacancyResponse>>(await Client.GetAsync("/vacancies"));
        var response = await Client.PostAsJsonAsync("/vacancies/ai-suggestions", request, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadAsync<VacancyAiResult>(response);
        Assert.Single(result.Questions);
        var after = await ReadAsync<List<HireFlow.Application.Vacancies.VacancyResponse>>(await Client.GetAsync("/vacancies"));
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public async Task VacancySuggestionsReportUnavailableWithoutModifyingAnything()
    {
        fixture.VacancyAssistant.Fail = true;
        try
        {
            var response = await Client.PostAsJsonAsync("/vacancies/ai-suggestions", new VacancyAiRequest { Title = "C# developer" }, Json);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally { fixture.VacancyAssistant.Fail = false; }
    }
    private async Task<(Guid VacancyId, Guid ApplicationId)> CreateBorderlineAsync()
    {
        var skill = Unique("skill");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();
        var response = await ApplyAsync(candidate.TelegramId, vacancy.Id, ResumeWith("other"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await ReadAsync<HireFlow.Application.JobApplications.ApplicationResponse>(response);
        Assert.Equal(ApplicationStatus.AiReview, result.Status);
        return (vacancy.Id, result.Id);
    }

    private async Task<bool> ProcessAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AiReviewProcessor>().ProcessOneAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReviewUsesRequirementsFromApplicationTime()
    {
        var (vacancyId, _) = await CreateBorderlineAsync();
        string originalSkill;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vacancy = await db.Vacancies.SingleAsync(v => v.Id == vacancyId);
            originalSkill = vacancy.Requirements.RequiredSkills.Single().Name;
            vacancy.Update(vacancy.Title, vacancy.Direction, vacancy.Description,
                new VacancyRequirements { RequiredSkills = [new Skill { Name = "changed" }], PassingScore = 70 }, 90);
            await db.SaveChangesAsync();
        }
        Assert.True(await ProcessAsync());
        Assert.Equal(originalSkill, fixture.Reviewer.LastRequirements!.RequiredSkills.Single().Name);
    }

    [Fact]
    public async Task DisablingAiReleasesAlreadyQueuedApplications()
    {
        var (_, id) = await CreateBorderlineAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var processor = new AiReviewProcessor(services.GetRequiredService<AppDbContext>(), fixture.Reviewer,
            Options.Create(new AiReviewOptions { Enabled = false }),
            Options.Create(new HireFlow.Application.JobApplications.QuestionOptions()),
            services.GetRequiredService<ILogger<AiReviewProcessor>>(), services.GetRequiredService<HireFlowMetrics>());
        Assert.True(await processor.ProcessOneAsync(CancellationToken.None));
        var result = await ReadAsync<HireFlow.Application.JobApplications.ApplicationResponse>(await Client.GetAsync($"/applications/{id}"));
        Assert.Equal(ApplicationStatus.RecruiterReview, result.Status);
        Assert.True(result.AiReviewUnavailable);
    }

    [Fact]
    public async Task BorderlineIsQueuedAndResultGoesToRecruiter()
    {
        var (_, id) = await CreateBorderlineAsync();
        Assert.True(await ProcessAsync());
        var result = await ReadAsync<HireFlow.Application.JobApplications.ApplicationResponse>(await Client.GetAsync($"/applications/{id}"));
        Assert.Equal(ApplicationStatus.RecruiterReview, result.Status);
        Assert.NotNull(result.AiReview);
        Assert.Equal("review", result.AiReview.Recommendation);
        Assert.False(result.AiReviewUnavailable);
        Assert.Empty(await GetNotificationsAsync(id));
        Assert.Contains(result.History, h => h.ChangedBy == StatusChangedBy.Ai);
    }

    [Fact]
    public async Task FailureIsRetriedThenSentToRecruiterWithoutRejection()
    {
        fixture.Reviewer.Fail = true;
        try
        {
            var (_, id) = await CreateBorderlineAsync();
            Assert.True(await ProcessAsync());
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var job = await db.AiReviewJobs.SingleAsync(j => j.ApplicationId == id);
                Assert.Equal(AiReviewJobStatus.Pending, job.Status);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_review_jobs SET available_at = now() WHERE application_id = {id}");
            }
            Assert.True(await ProcessAsync());
            var result = await ReadAsync<HireFlow.Application.JobApplications.ApplicationResponse>(await Client.GetAsync($"/applications/{id}"));
            Assert.Equal(ApplicationStatus.RecruiterReview, result.Status);
            Assert.True(result.AiReviewUnavailable);
            Assert.Null(result.AiReview);
            Assert.Empty(await GetNotificationsAsync(id));
        }
        finally { fixture.Reviewer.Fail = false; }
    }

    [Fact]
    public async Task ParallelWorkersSkipLockedJob()
    {
        await CreateBorderlineAsync();
        fixture.Reviewer.Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Reviewer.Release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var first = ProcessAsync();
            await fixture.Reviewer.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(await ProcessAsync());
            fixture.Reviewer.Release.TrySetResult();
            Assert.True(await first);
        }
        finally { fixture.Reviewer.Release.TrySetResult(); fixture.Reviewer.Started = null; fixture.Reviewer.Release = null; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuestionsAreAnsweredAndReviewedAfterScreening(bool borderline)
    {
        var skill = Unique("skill");
        var request = VacancyRequestFor([skill]) with { Questions = [new() { Text = "Формат работы?", Criterion = "удалённо" }, new() { Text = "Когда готовы начать?" }] };
        var created = await Client.PostAsJsonAsync("/vacancies", request, Json);
        var vacancy = await ReadAsync<HireFlow.Application.Vacancies.VacancyResponse>(created);
        var candidate = await CreateCandidateAsync();
        fixture.Reviewer.Recommendation = "pass";
        try
        {
            var application = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith(borderline ? "other" : skill));
            Assert.Equal(ApplicationStatus.AiReview, application.Status);
            Assert.True(await ProcessAsync());
            var due = await GetDueAsync();
            Assert.Contains(due, n => n.ApplicationId == application.Id && n.Type == HireFlow.Domain.Notifications.NotificationType.QuestionsReady && n.Question == "Формат работы?");
            var pending = await ReadAsync<HireFlow.Application.JobApplications.PendingQuestionResponse>(await Client.GetAsync($"/internal/candidates/{candidate.TelegramId}/questions"));
            Assert.Equal(0, pending.Position);
            var wrong = await Client.PostAsJsonAsync($"/internal/candidates/{candidate.TelegramId}/questions/{application.Id}", new { position = 1, answer = "удалённо" }, Json);
            Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
            var first = await Client.PostAsJsonAsync($"/internal/candidates/{candidate.TelegramId}/questions/{application.Id}", new { position = 0, answer = "удалённо" }, Json);
            var firstBody = await ReadAsync<HireFlow.Application.JobApplications.AnswerQuestionResponse>(first);
            Assert.False(firstBody.Completed);
            Assert.Equal("Когда готовы начать?", firstBody.Next!.Question);
            var second = await Client.PostAsJsonAsync($"/internal/candidates/{candidate.TelegramId}/questions/{application.Id}", new { position = 1, answer = "через месяц" }, Json);
            Assert.True((await ReadAsync<HireFlow.Application.JobApplications.AnswerQuestionResponse>(second)).Completed);
            Assert.True(await ProcessAsync());
            var result = await ReadAsync<HireFlow.Application.JobApplications.ApplicationResponse>(await Client.GetAsync($"/applications/{application.Id}"));
            Assert.Equal(ApplicationStatus.RecruiterReview, result.Status);
            Assert.Equal("match", result.AnswersReview!.Recommendation);
            Assert.Equal("удалённо", result.Questions![0].Answer);
        }
        finally { fixture.Reviewer.Recommendation = "review"; }
    }

    [Fact]
    public async Task UnansweredQuestionsExpireWithoutRejectingCandidate()
    {
        var skill = Unique("skill");
        var request = VacancyRequestFor([skill]) with { Questions = [new() { Text = "Какой график?" }] };
        var vacancy = await ReadAsync<HireFlow.Application.Vacancies.VacancyResponse>(await Client.PostAsJsonAsync("/vacancies", request, Json));
        var candidate = await CreateCandidateAsync();
        fixture.Reviewer.Recommendation = "pass";
        var application = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith(skill));
        Assert.True(await ProcessAsync());
        fixture.Reviewer.Recommendation = "review";
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE applications SET questions_deadline = now() - interval '1 minute' WHERE id = {application.Id}");
        await scope.ServiceProvider.GetRequiredService<HireFlow.Application.JobApplications.QuestionnaireService>().ExpireOverdueAsync(CancellationToken.None);
        var result = await ReadAsync<HireFlow.Application.JobApplications.ApplicationResponse>(await Client.GetAsync($"/applications/{application.Id}"));
        Assert.Equal(ApplicationStatus.Expired, result.Status);
        Assert.DoesNotContain(await GetNotificationsAsync(application.Id), n => n.Status == HireFlow.Domain.Notifications.NotificationStatus.Pending);
    }

    [Fact]
    public async Task MultipleQuestionnairesAreOfferedOneAtATime()
    {
        var skill = Unique("skill");
        var request = VacancyRequestFor([skill]) with { Questions = [new() { Text = "Какой формат?" }] };
        var firstVacancy = await ReadAsync<HireFlow.Application.Vacancies.VacancyResponse>(await Client.PostAsJsonAsync("/vacancies", request, Json));
        var secondVacancy = await ReadAsync<HireFlow.Application.Vacancies.VacancyResponse>(await Client.PostAsJsonAsync("/vacancies", request, Json));
        var candidate = await CreateCandidateAsync();
        fixture.Reviewer.Recommendation = "pass";
        var first = await ApplyOkAsync(candidate.TelegramId, firstVacancy.Id, ResumeWith(skill));
        var second = await ApplyOkAsync(candidate.TelegramId, secondVacancy.Id, ResumeWith(skill));
        Assert.True(await ProcessAsync());
        Assert.True(await ProcessAsync());
        fixture.Reviewer.Recommendation = "review";
        var due = await GetDueAsync();
        Assert.Contains(due, n => n.ApplicationId == first.Id && n.Type == HireFlow.Domain.Notifications.NotificationType.QuestionsReady);
        Assert.DoesNotContain(due, n => n.ApplicationId == second.Id && n.Type == HireFlow.Domain.Notifications.NotificationType.QuestionsReady);
        var answer = await Client.PostAsJsonAsync($"/internal/candidates/{candidate.TelegramId}/questions/{first.Id}", new { position = 0, answer = "Удалённо" }, Json);
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.True(await ProcessAsync());
        due = await GetDueAsync();
        Assert.Contains(due, n => n.ApplicationId == second.Id && n.Type == HireFlow.Domain.Notifications.NotificationType.QuestionsReady);
    }

    [Fact]
    public async Task PassedWithoutQuestionsIsReviewedByAiThenSentToRecruiter()
    {
        var skill = Unique("skill");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();
        fixture.Reviewer.Recommendation = "pass";
        try
        {
            var application = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith(skill));
            Assert.Equal(ApplicationStatus.AiReview, application.Status);
            Assert.True(await ProcessAsync());
            var result = await ReadAsync<HireFlow.Application.JobApplications.ApplicationResponse>(await Client.GetAsync($"/applications/{application.Id}"));
            Assert.Equal(ApplicationStatus.RecruiterReview, result.Status);
            Assert.Equal("pass", result.AiReview!.Recommendation);
            Assert.Empty(await GetNotificationsAsync(application.Id));
        }
        finally { fixture.Reviewer.Recommendation = "review"; }
    }
}
