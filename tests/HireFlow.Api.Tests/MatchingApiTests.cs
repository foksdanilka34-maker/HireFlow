using System.Net;
using System.Net.Http.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Matching;

namespace HireFlow.Api.Tests;

public class MatchingApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    private async Task<List<Guid>> MatchIdsAsync(long telegramId, byte[] resume, int years = 5)
    {
        var response = await MatchesAsync(telegramId, resume, years);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync<List<VacancyMatchResponse>>(response)).Select(m => m.Id).ToList();
    }

    [Fact]
    public async Task Matches_ReturnsOnlyPassedVacanciesSortedByScore()
    {
        var (s1, n1, n2, s2, s3, n3) = (Unique("s"), Unique("n"), Unique("n"), Unique("s"), Unique("s"), Unique("n"));
        var score90 = await CreateVacancyAsync([s1], [n1, n2]);   // 80 + 10 = 90
        var score100 = await CreateVacancyAsync([s2]);            // 100
        var score80 = await CreateVacancyAsync([s3], [n3]);       // 80
        var resume = ResumeWith(s1, n1, s2, s3);

        var response = await MatchesAsync(NewTelegramId(), resume);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = (await ReadAsync<List<VacancyMatchResponse>>(response)).Select(m => m.Id).ToList();
        Assert.Equal(
            [score100.Id, score90.Id, score80.Id],
            ids.Where(id => id == score100.Id || id == score90.Id || id == score80.Id).ToList());
    }

    [Fact]
    public async Task Matches_ReturnsOnlyPublicFieldsOfVacancy()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill], title: Unique("Title "));

        var response = await MatchesAsync(NewTelegramId(), ResumeWith(skill));

        var match = (await ReadAsync<List<VacancyMatchResponse>>(response)).Single(m => m.Id == vacancy.Id);
        Assert.Equal(vacancy.Title, match.Title);
        Assert.Equal("QA", match.Direction);
        Assert.Equal("Тестовая вакансия", match.Description);
        var raw = await (await MatchesAsync(NewTelegramId(), ResumeWith(skill))).Content.ReadAsStringAsync();
        Assert.DoesNotContain("score", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requirements", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Matches_DoNotIncludeBorderlineVacancies()
    {
        var (found, missing) = (Unique("s"), Unique("s"));
        // Не хватает одного навыка из двух: пограничный, но не «прошёл».
        var borderline = await CreateVacancyAsync([found, missing]);

        var ids = await MatchIdsAsync(NewTelegramId(), ResumeWith(found));

        Assert.DoesNotContain(borderline.Id, ids);
    }

    [Fact]
    public async Task Matches_DoNotIncludeVacancyWhenExperienceIsInsufficient()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill], minYears: 5);

        var ids = await MatchIdsAsync(NewTelegramId(), ResumeWith(skill), years: 2);

        Assert.DoesNotContain(vacancy.Id, ids);
    }

    [Fact]
    public async Task Matches_DoNotIncludeClosedAndArchivedVacancies()
    {
        var skill = Unique("s");
        var open = await CreateVacancyAsync([skill]);
        var closed = await CreateVacancyAsync([skill]);
        var archived = await CreateVacancyAsync([skill]);
        (await Client.PostAsync($"/vacancies/{closed.Id}/close", null)).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/vacancies/{archived.Id}/archive", null)).EnsureSuccessStatusCode();

        var ids = await MatchIdsAsync(NewTelegramId(), ResumeWith(skill));

        Assert.Contains(open.Id, ids);
        Assert.DoesNotContain(closed.Id, ids);
        Assert.DoesNotContain(archived.Id, ids);
    }

    [Fact]
    public async Task Matches_DoNotIncludeVacanciesCandidateAlreadyAppliedTo()
    {
        var skill = Unique("s");
        var applied = await CreateVacancyAsync([skill]);
        var fresh = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();
        var resume = ResumeWith(skill);
        await ApplyOkAsync(candidate.TelegramId, applied.Id, resume);

        var ids = await MatchIdsAsync(candidate.TelegramId, resume);

        Assert.DoesNotContain(applied.Id, ids);
        Assert.Contains(fresh.Id, ids);
    }

    [Fact]
    public async Task Matches_HideVacancyAfterRejectedApplicationToo()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();
        var application = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith(skill));
        (await DecideAsync(application.Id, "reject")).EnsureSuccessStatusCode();

        var ids = await MatchIdsAsync(candidate.TelegramId, ResumeWith(skill));

        Assert.DoesNotContain(vacancy.Id, ids);
    }

    [Fact]
    public async Task Matches_ShowVacancyToOtherCandidateWhoDidNotApply()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var applicant = await CreateCandidateAsync();
        await ApplyOkAsync(applicant.TelegramId, vacancy.Id, ResumeWith(skill));

        var ids = await MatchIdsAsync(NewTelegramId(), ResumeWith(skill));

        Assert.Contains(vacancy.Id, ids);
    }

    [Fact]
    public async Task Matches_SaveNothing()
    {
        var skill = Unique("s");
        await CreateVacancyAsync([skill]);
        var telegramId = NewTelegramId();
        var applicationsBefore = await CountApplicationsAsync();
        var filesBefore = StoredFileCount();

        var response = await MatchesAsync(telegramId, ResumeWith(skill));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(applicationsBefore, await CountApplicationsAsync());
        Assert.Equal(filesBefore, StoredFileCount());
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/candidates/{telegramId}")).StatusCode);
    }

    [Fact]
    public async Task Matches_WorkWithDocx()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);

        var ids = await MatchIdsAsync(NewTelegramId(), ResumeFactory.Docx("Резюме", $"Навыки: {skill}"), 5);

        Assert.Contains(vacancy.Id, ids);
    }

    [Fact]
    public async Task Matches_NothingMatchesReturnsEmptyListForOwnVacancies()
    {
        var vacancy = await CreateVacancyAsync([Unique("s")]);

        var ids = await MatchIdsAsync(NewTelegramId(), ResumeWith("nothing", "relevant"));

        Assert.DoesNotContain(vacancy.Id, ids);
    }

    [Theory]
    [MemberData(nameof(BadFiles))]
    public async Task Matches_UnsupportedFileReturns400(string name, byte[] file)
    {
        var response = await MatchesAsync(NewTelegramId(), file, fileName: name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public static TheoryData<string, byte[]> BadFiles() => new()
    {
        { "resume.txt", System.Text.Encoding.UTF8.GetBytes("просто текст") },
        { "resume.doc", ResumeFactory.OldDoc() },
        { "resume.pdf", [] },
        { "scan.pdf", ResumeFactory.EmptyPdf() },
        { "broken.pdf", [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x00, 0x01] },
        { "broken.docx", [0x50, 0x4B, 0x03, 0x04, 0x00, 0x01, 0x02] },
    };

    [Fact]
    public async Task Matches_YearsOutOfRangeReturns400()
    {
        var response = await MatchesAsync(NewTelegramId(), ResumeWith("x"), years: 51);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
