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
        return (await ReadAsync<FindMatchesResponse>(response)).Vacancies.Select(m => m.Id).ToList();
    }

    [Fact]
    public async Task Matches_ReturnsOnlyPassedVacanciesSortedByScore()
    {
        var (s1, n1, n2, s2, s3, n3) = (Unique("s"), Unique("n"), Unique("n"), Unique("s"), Unique("s"), Unique("n"));
        var score90 = await CreateVacancyAsync([s1], [n1, n2]);   // 80 + 10 = 90
        var score100 = await CreateVacancyAsync([s2]);            // 100
        var score80 = await CreateVacancyAsync([s3], [n3]);       // 80
        // Не хватает одного навыка из двух: пограничный, но не «прошёл» — в подбор не попадает.
        var borderline = await CreateVacancyAsync([s1, Unique("s")]);
        var resume = ResumeWith(s1, n1, s2, s3);

        var response = await MatchesAsync(NewTelegramId(), resume);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = (await ReadAsync<FindMatchesResponse>(response)).Vacancies.Select(m => m.Id).ToList();
        Assert.Equal(
            [score100.Id, score90.Id, score80.Id],
            ids.Where(id => id == score100.Id || id == score90.Id || id == score80.Id).ToList());
        Assert.DoesNotContain(borderline.Id, ids);
    }

    [Fact]
    public async Task Matches_ReturnsOnlyPublicFieldsOfVacancy()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill], title: Unique("Title "));

        var response = await MatchesAsync(NewTelegramId(), ResumeWith(skill));

        var match = (await ReadAsync<FindMatchesResponse>(response)).Vacancies.Single(m => m.Id == vacancy.Id);
        Assert.Equal(vacancy.Title, match.Title);
        Assert.Equal("QA", match.Direction);
        Assert.Equal("Тестовая вакансия", match.Description);
        var raw = await (await MatchesAsync(NewTelegramId(), ResumeWith(skill))).Content.ReadAsStringAsync();
        Assert.DoesNotContain("score", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requirements", raw, StringComparison.OrdinalIgnoreCase);
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
        // Другой кандидат, который не откликался, вакансию видит.
        Assert.Contains(applied.Id, await MatchIdsAsync(NewTelegramId(), resume));
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
    public async Task Matches_UnsupportedFileReturns400()
    {
        var response = await MatchesAsync(NewTelegramId(), ResumeFactory.OldDoc(), fileName: "resume.doc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Запрашивает подбор без поля yearsOfExperience: опыт должен найтись в резюме.
    private Task<HttpResponseMessage> MatchesWithoutYearsAsync(long telegramId, byte[] file, string fileName = "resume.docx")
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(file), "file", fileName } };
        return Client.PostAsync($"/candidates/{telegramId}/matches", form);
    }

    [Theory]
    [InlineData("Опыт работы — 5 лет 3 месяца", 5, true)]
    [InlineData("Опыт работы — 2 года", 2, false)]
    public async Task Matches_WithoutYearsTakesExperienceFromResumeAndUsesItForMatching(string statement, int years, bool matched)
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill], minYears: 5);

        var response = await MatchesWithoutYearsAsync(NewTelegramId(), ResumeFactory.Docx(statement, $"Навыки: {skill}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadAsync<FindMatchesResponse>(response);
        Assert.Equal(years, result.YearsOfExperience);
        Assert.False(result.ExperienceRequired);
        Assert.Equal(matched, result.Vacancies.Any(v => v.Id == vacancy.Id));
    }

    [Fact]
    public async Task Matches_WithoutYearsAndWithoutStatementRequiresExperienceAndReturnsNothing()
    {
        var skill = Unique("s");
        await CreateVacancyAsync([skill]);

        var response = await MatchesWithoutYearsAsync(NewTelegramId(), ResumeFactory.Docx("Работал программистом", $"Навыки: {skill}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadAsync<FindMatchesResponse>(response);
        Assert.Null(result.YearsOfExperience);
        Assert.True(result.ExperienceRequired);
        Assert.Empty(result.Vacancies);
    }

    [Theory]
    [InlineData("Тел.: +7 (999) 123-45-67", true)]
    [InlineData("Контакты: ivan@example.com, 2019–2023", false)]
    public async Task Matches_ReportsPhoneFoundWithoutReturningThePhone(string contacts, bool expected)
    {
        var response = await MatchesWithoutYearsAsync(
            NewTelegramId(), ResumeFactory.Docx("Опыт работы: 5 лет", contacts));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected, (await ReadAsync<FindMatchesResponse>(response)).PhoneFound);
        Assert.DoesNotContain("9991234567", raw);
    }
}
