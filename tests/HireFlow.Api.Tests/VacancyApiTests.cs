using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Vacancies;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Api.Tests;

public class VacancyApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    // Читает список вакансий с нужным фильтром и возвращает id.
    private async Task<List<Guid>> ListIdsAsync(string query = "")
    {
        var response = await Client.GetAsync("/vacancies" + query);
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<List<VacancyResponse>>(response)).Select(v => v.Id).ToList();
    }

    [Fact]
    public async Task Create_Returns201WithLocationAndOpenStatusInSnakeCase()
    {
        var request = VacancyRequestFor([Unique("skill")]);

        var response = await Client.PostAsJsonAsync("/vacancies", request, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("open", json.RootElement.GetProperty("status").GetString());
        Assert.Equal($"/vacancies/{id}", response.Headers.Location!.OriginalString);
        Assert.Equal(7, id.Version);
    }

    [Fact]
    public async Task Create_EmptyRequiredSkillsReturns400()
    {
        var request = VacancyRequestFor([]);

        var response = await Client.PostAsJsonAsync("/vacancies", request, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_BorderlineWidthGreaterThanPassingScoreReturns400()
    {
        var request = VacancyRequestFor([Unique("skill")], passingScore: 20, borderlineWidth: 30);

        var response = await Client.PostAsJsonAsync("/vacancies", request, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownIdReturns404()
    {
        var response = await Client.GetAsync($"/vacancies/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_UnknownStatusReturns400()
    {
        var response = await Client.GetAsync("/vacancies?status=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_ExcludesArchivedByDefaultAndFiltersByStatus()
    {
        var open = await CreateVacancyAsync([Unique("skill")]);
        var closed = await CreateVacancyAsync([Unique("skill")]);
        var archived = await CreateVacancyAsync([Unique("skill")]);
        (await Client.PostAsync($"/vacancies/{closed.Id}/close", null)).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/vacancies/{archived.Id}/archive", null)).EnsureSuccessStatusCode();

        var all = await ListIdsAsync();
        var openIds = await ListIdsAsync("?status=open");
        var archivedIds = await ListIdsAsync("?status=archived");

        Assert.Contains(open.Id, all);
        Assert.Contains(closed.Id, all);
        Assert.DoesNotContain(archived.Id, all);
        Assert.Contains(open.Id, openIds);
        Assert.DoesNotContain(closed.Id, openIds);
        Assert.DoesNotContain(archived.Id, openIds);
        Assert.Contains(archived.Id, archivedIds);
        Assert.DoesNotContain(open.Id, archivedIds);
    }

    [Fact]
    public async Task Requirements_ArePersistedAndReadBack()
    {
        var required = Unique("req");
        var nice = Unique("nice");
        var request = VacancyRequestFor([required], [nice], minYears: 3, passingScore: 75, borderlineWidth: 15) with
        {
            Requirements = new VacancyRequirements
            {
                RequiredSkills = [new Skill { Name = required, Synonyms = ["syn1", "syn2"] }],
                NiceToHaveSkills = [new Skill { Name = nice, Synonyms = ["niceSyn"] }],
                MinYearsOfExperience = 3,
                PassingScore = 75,
                BorderlineWidth = 15,
            },
        };
        var created = await ReadAsync<VacancyResponse>(await Client.PostAsJsonAsync("/vacancies", request, Json));

        var loaded = await ReadAsync<VacancyResponse>(await Client.GetAsync($"/vacancies/{created.Id}"));

        var requirements = loaded.Requirements;
        var requiredSkill = Assert.Single(requirements.RequiredSkills);
        Assert.Equal(required, requiredSkill.Name);
        Assert.Equal(["syn1", "syn2"], requiredSkill.Synonyms);
        var niceSkill = Assert.Single(requirements.NiceToHaveSkills);
        Assert.Equal(nice, niceSkill.Name);
        Assert.Equal(["niceSyn"], niceSkill.Synonyms);
        Assert.Equal(3, requirements.MinYearsOfExperience);
        Assert.Equal(75, requirements.PassingScore);
        Assert.Equal(15, requirements.BorderlineWidth);
        Assert.Equal(90, loaded.FreezePeriodDays);
    }

    [Fact]
    public async Task Update_ReplacesContent()
    {
        var vacancy = await CreateVacancyAsync([Unique("skill")]);
        var newSkill = Unique("new");
        var update = VacancyRequestFor([newSkill], passingScore: 60, title: Unique("Updated "));

        var response = await Client.PutAsJsonAsync($"/vacancies/{vacancy.Id}", update, Json);

        response.EnsureSuccessStatusCode();
        var loaded = await ReadAsync<VacancyResponse>(await Client.GetAsync($"/vacancies/{vacancy.Id}"));
        Assert.Equal(update.Title, loaded.Title);
        Assert.Equal(newSkill, Assert.Single(loaded.Requirements.RequiredSkills).Name);
        Assert.Equal(60, loaded.Requirements.PassingScore);
    }

    [Fact]
    public async Task CloseAndOpen_ChangeStatus()
    {
        var vacancy = await CreateVacancyAsync([Unique("skill")]);

        var closed = await ReadAsync<VacancyResponse>(await Client.PostAsync($"/vacancies/{vacancy.Id}/close", null));
        var reopened = await ReadAsync<VacancyResponse>(await Client.PostAsync($"/vacancies/{vacancy.Id}/open", null));

        Assert.Equal(VacancyStatus.Closed, closed.Status);
        Assert.Equal(VacancyStatus.Open, reopened.Status);
    }

    [Fact]
    public async Task Archived_UpdateReturns409()
    {
        var vacancy = await CreateVacancyAsync([Unique("skill")]);
        (await Client.PostAsync($"/vacancies/{vacancy.Id}/archive", null)).EnsureSuccessStatusCode();

        var response = await Client.PutAsJsonAsync($"/vacancies/{vacancy.Id}", VacancyRequestFor([Unique("skill")]), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Unarchive_MakesVacancyClosed()
    {
        var vacancy = await CreateVacancyAsync([Unique("skill")]);
        (await Client.PostAsync($"/vacancies/{vacancy.Id}/archive", null)).EnsureSuccessStatusCode();

        var response = await Client.PostAsync($"/vacancies/{vacancy.Id}/unarchive", null);

        Assert.Equal(VacancyStatus.Closed, (await ReadAsync<VacancyResponse>(response)).Status);
    }

    [Fact]
    public async Task Delete_WithoutApplicationsReturns204AndRemovesVacancy()
    {
        var vacancy = await CreateVacancyAsync([Unique("skill")]);

        var response = await Client.DeleteAsync($"/vacancies/{vacancy.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/vacancies/{vacancy.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_WithApplicationReturns409AndKeepsVacancy()
    {
        var skill = Unique("skill");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();
        await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith(skill));

        var response = await Client.DeleteAsync($"/vacancies/{vacancy.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/vacancies/{vacancy.Id}")).StatusCode);
    }
}
