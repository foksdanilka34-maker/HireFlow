using System.Net;
using System.Text.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.JobApplications;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using HireFlow.Domain.Screening;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

public class ApplicationApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    // Читает ключ файла резюме отклика прямо из БД.
    private async Task<string> ResumeFileKeyAsync(Guid applicationId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.JobApplications.AsNoTracking().Where(a => a.Id == applicationId)
            .Select(a => a.ResumeFileKey).SingleAsync();
    }

    [Fact]
    public async Task Create_PassedGoesToRecruiterReviewWithTwoHistoryEntriesAndNoNotifications()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();

        var response = await ApplyAsync(candidate.TelegramId, vacancy.Id, ResumeWith(skill), fileName: "my-resume.pdf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var application = JsonSerializer.Deserialize<ApplicationResponse>(raw, Json)!;
        using (var json = JsonDocument.Parse(raw))
        {
            // Перечисления в JSON пишутся в snake_case.
            Assert.Equal("recruiter_review", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("passed", json.RootElement.GetProperty("screening").GetProperty("verdict").GetString());
        }
        Assert.Equal($"/applications/{application.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal(ApplicationStatus.RecruiterReview, application.Status);
        Assert.Equal(ScreeningVerdict.Passed, application.Screening.Verdict);
        Assert.Equal(100, application.Screening.Score);
        Assert.Equal(vacancy.Id, application.VacancyId);
        Assert.Equal(candidate.Id, application.CandidateId);
        Assert.Equal("my-resume.pdf", application.ResumeFileName);

        Assert.Equal(2, application.History.Count);
        Assert.Null(application.History[0].FromStatus);
        Assert.Equal(ApplicationStatus.Received, application.History[0].ToStatus);
        Assert.Equal(StatusChangedBy.System, application.History[0].ChangedBy);
        Assert.Equal(ApplicationStatus.Received, application.History[1].FromStatus);
        Assert.Equal(ApplicationStatus.RecruiterReview, application.History[1].ToStatus);
        Assert.Equal(StatusChangedBy.System, application.History[1].ChangedBy);

        Assert.Empty(await GetNotificationsAsync(application.Id));
    }

    [Fact]
    public async Task Create_SavesResumeFileInStorageWithOriginalBytes()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();
        var resume = ResumeWith(skill);
        var filesBefore = StoredFileCount();

        var application = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, resume);

        var key = await ResumeFileKeyAsync(application.Id);
        var path = Path.Combine(App.StoragePath, key);
        Assert.True(File.Exists(path));
        Assert.Equal(resume, await File.ReadAllBytesAsync(path));
        Assert.Equal(filesBefore + 1, StoredFileCount());
    }

    [Fact]
    public async Task Create_DocxResumeIsScreened()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();

        var application = await ApplyOkAsync(
            candidate.TelegramId, vacancy.Id, ResumeFactory.Docx("Резюме", skill), fileName: "resume.docx");

        Assert.Equal(ApplicationStatus.RecruiterReview, application.Status);
        Assert.Empty(application.Screening.MissingRequiredSkills);
    }

    [Fact]
    public async Task Create_FailedIsAutoRejectedWithPendingDelayedRejectionNotification()
    {
        var vacancy = await CreateVacancyAsync([Unique("s"), Unique("s")]);
        var candidate = await CreateCandidateAsync();
        var before = DateTime.UtcNow;

        var application = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith("irrelevant", "words"));

        Assert.Equal(ApplicationStatus.AutoRejected, application.Status);
        Assert.Equal(ScreeningVerdict.Failed, application.Screening.Verdict);
        Assert.Equal(0, application.Screening.Score);
        Assert.Equal(2, application.Screening.MissingRequiredSkills.Count);
        Assert.Equal(ApplicationStatus.AutoRejected, application.History[^1].ToStatus);
        Assert.Equal(StatusChangedBy.System, application.History[^1].ChangedBy);

        var notification = Assert.Single(await GetNotificationsAsync(application.Id));
        Assert.Equal(NotificationType.Rejected, notification.Type);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.InRange(notification.SendAfter, before + ApiFactory.RejectionDelay, DateTime.UtcNow + ApiFactory.RejectionDelay);
        Assert.DoesNotContain(await GetDueAsync(), d => d.Id == notification.Id);
    }

    [Fact]
    public async Task Create_UnknownVacancyReturns404()
    {
        var candidate = await CreateCandidateAsync();

        var response = await ApplyAsync(candidate.TelegramId, Guid.NewGuid(), ResumeWith("x"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ClosedVacancyReturns409AndSavesNothing()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        (await Client.PostAsync($"/vacancies/{vacancy.Id}/close", null)).EnsureSuccessStatusCode();
        var candidate = await CreateCandidateAsync();
        var filesBefore = StoredFileCount();

        var response = await ApplyAsync(candidate.TelegramId, vacancy.Id, ResumeWith(skill));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountApplicationsAsync(vacancy.Id));
        Assert.Equal(filesBefore, StoredFileCount());
    }

    [Fact]
    public async Task Create_DuplicateApplicationReturns409BeforeReadingResumeAndCreatesNothing()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();
        var first = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith(skill));
        var filesAfterFirst = StoredFileCount();

        // Файл заведомо непригоден, но ответ 409, а не 400: дубль проверяется раньше файла.
        var response = await ApplyAsync(candidate.TelegramId, vacancy.Id, ResumeFactory.OldDoc());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await CountApplicationsAsync(vacancy.Id));
        Assert.Equal(filesAfterFirst, StoredFileCount());
        var loaded = await ReadAsync<ApplicationResponse>(await Client.GetAsync($"/applications/{first.Id}"));
        Assert.Equal(ApplicationStatus.RecruiterReview, loaded.Status);
        Assert.Equal(2, loaded.History.Count);
    }

    [Fact]
    public async Task Create_SameCandidateCanApplyToAnotherVacancyAndAnotherCandidateToSame()
    {
        var skill = Unique("s");
        var vacancy1 = await CreateVacancyAsync([skill]);
        var vacancy2 = await CreateVacancyAsync([skill]);
        var candidate1 = await CreateCandidateAsync();
        var candidate2 = await CreateCandidateAsync();

        await ApplyOkAsync(candidate1.TelegramId, vacancy1.Id, ResumeWith(skill));
        await ApplyOkAsync(candidate1.TelegramId, vacancy2.Id, ResumeWith(skill));
        await ApplyOkAsync(candidate2.TelegramId, vacancy1.Id, ResumeWith(skill));

        Assert.Equal(2, await CountApplicationsAsync(vacancy1.Id));
        Assert.Equal(1, await CountApplicationsAsync(vacancy2.Id));
    }

    [Fact]
    public async Task Create_UnsupportedFileReturns400AndSavesNothing()
    {
        var vacancy = await CreateVacancyAsync([Unique("s")]);
        var candidate = await CreateCandidateAsync();
        var filesBefore = StoredFileCount();

        var response = await ApplyAsync(candidate.TelegramId, vacancy.Id, ResumeFactory.OldDoc(), fileName: "resume.doc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountApplicationsAsync(vacancy.Id));
        Assert.Equal(filesBefore, StoredFileCount());
    }

    [Fact]
    public async Task Get_ReturnsApplicationWithHistoryInOrder()
    {
        var vacancy = await CreateVacancyAsync([Unique("s"), Unique("s")]);
        var candidate = await CreateCandidateAsync();
        var created = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith("nothing"));
        (await DecideAsync(created.Id, "invite", "всё же приглашаем")).EnsureSuccessStatusCode();

        var response = await Client.GetAsync($"/applications/{created.Id}");

        var application = await ReadAsync<ApplicationResponse>(response);
        Assert.Equal(created.Id, application.Id);
        Assert.Equal(ApplicationStatus.Invited, application.Status);
        Assert.Equal(
            [
                (null, ApplicationStatus.Received),
                (ApplicationStatus.Received, ApplicationStatus.AutoRejected),
                (ApplicationStatus.AutoRejected, ApplicationStatus.Invited),
            ],
            application.History.Select(h => (h.FromStatus, h.ToStatus)).ToArray());
        Assert.True(application.History.Zip(application.History.Skip(1)).All(p => p.First.ChangedAt <= p.Second.ChangedAt));
    }

    [Fact]
    public async Task Get_UnknownIdReturns404()
    {
        var response = await Client.GetAsync($"/applications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByVacancyIdAndStatus()
    {
        var (skill1, skill2) = (Unique("s"), Unique("s"));
        var vacancy = await CreateVacancyAsync([skill1, skill2], title: Unique("List "));
        var other = await CreateVacancyAsync([skill1]);
        var good = await CreateCandidateAsync();
        var bad = await CreateCandidateAsync();
        var passed = await ApplyOkAsync(good.TelegramId, vacancy.Id, ResumeWith(skill1, skill2));
        var failed = await ApplyOkAsync(bad.TelegramId, vacancy.Id, ResumeWith("nothing"));
        var inOther = await ApplyOkAsync(good.TelegramId, other.Id, ResumeWith(skill1));

        var all = await ReadAsync<List<ApplicationListItem>>(await Client.GetAsync($"/applications?vacancyId={vacancy.Id}"));
        var review = await ReadAsync<List<ApplicationListItem>>(
            await Client.GetAsync($"/applications?vacancyId={vacancy.Id}&status=recruiter_review"));
        var rejected = await ReadAsync<List<ApplicationListItem>>(
            await Client.GetAsync($"/applications?vacancyId={vacancy.Id}&status=auto_rejected"));

        Assert.Equal(new[] { passed.Id, failed.Id }.Order(), all.Select(a => a.Id).Order());
        Assert.DoesNotContain(all, a => a.Id == inOther.Id);
        Assert.Equal(passed.Id, Assert.Single(review).Id);
        Assert.Equal(failed.Id, Assert.Single(rejected).Id);
        var item = all.Single(a => a.Id == passed.Id);
        Assert.Equal(vacancy.Title, item.VacancyTitle);
        Assert.Equal(good.Name, item.CandidateName);
        Assert.Equal(ScreeningVerdict.Passed, item.Verdict);
        Assert.Equal(100, item.Score);
    }

    [Fact]
    public async Task List_UnknownStatusReturns400()
    {
        var response = await Client.GetAsync("/applications?status=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
