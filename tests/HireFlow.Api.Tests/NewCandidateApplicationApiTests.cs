using System.Net;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Candidates;

namespace HireFlow.Api.Tests;

// Отклик, который сам создаёт кандидата: имя из формы, телефон из формы или из резюме.
public class NewCandidateApplicationApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    private Task<HttpResponseMessage> ApplyAsNewAsync(
        long telegramId, Guid vacancyId, byte[] resume, string? name, string? phone)
    {
        var fields = new List<(string, string)> { ("telegramId", telegramId.ToString()), ("vacancyId", vacancyId.ToString()) };
        if (name is not null)
            fields.Add(("name", name));
        if (phone is not null)
            fields.Add(("phone", phone));
        return Client.PostAsync("/applications", ResumeForm(resume, "resume.docx", 5, [.. fields]));
    }

    private async Task<CandidateResponse> GetCandidateAsync(long telegramId)
    {
        var response = await Client.GetAsync($"/candidates/{telegramId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<CandidateResponse>(response);
    }

    [Fact]
    public async Task Create_NewCandidateGetsNameFromFormAndPhoneFromResume()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var telegramId = NewTelegramId();
        var resume = ResumeFactory.Docx($"Навыки: {skill}", "Тел.: 8 (999) 123-45-67");

        var response = await ApplyAsNewAsync(telegramId, vacancy.Id, resume, "Иван Петров", phone: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var application = await ReadAsync<HireFlow.Application.JobApplications.ApplicationResponse>(response);
        var candidate = await GetCandidateAsync(telegramId);
        Assert.Equal("Иван Петров", candidate.Name);
        Assert.Equal("+79991234567", candidate.Phone);
        Assert.Equal(candidate.Id, application.CandidateId);
    }

    [Fact]
    public async Task Create_ExplicitPhoneWinsOverPhoneInResume()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var telegramId = NewTelegramId();
        var resume = ResumeFactory.Docx($"Навыки: {skill}", "Тел.: +7 999 123-45-67");

        var response = await ApplyAsNewAsync(telegramId, vacancy.Id, resume, "Иван", phone: "+44 20 7946 0958");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("+442079460958", (await GetCandidateAsync(telegramId)).Phone);
    }

    [Theory]
    [InlineData("Иван", null)]
    [InlineData("Иван", "abc")]
    [InlineData(null, "+79991234567")]
    public async Task Create_NewCandidateWithoutNameOrValidPhoneReturns400AndCreatesNothing(string? name, string? phone)
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var telegramId = NewTelegramId();
        var filesBefore = StoredFileCount();

        var response = await ApplyAsNewAsync(
            telegramId, vacancy.Id, ResumeFactory.Docx($"Навыки: {skill}", "Работал 2019–2023"), name, phone);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/candidates/{telegramId}")).StatusCode);
        Assert.Equal(0, await CountApplicationsAsync(vacancy.Id));
        Assert.Equal(filesBefore, StoredFileCount());
    }

    [Fact]
    public async Task Create_ExistingCandidateKeepsNameAndPhone()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var existing = await CreateCandidateAsync();
        var resume = ResumeFactory.Docx($"Навыки: {skill}", "Тел.: +7 888 555-44-33");

        var response = await ApplyAsNewAsync(existing.TelegramId, vacancy.Id, resume, "Другое Имя", phone: "+44 20 7946 0958");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var candidate = await GetCandidateAsync(existing.TelegramId);
        Assert.Equal(existing.Name, candidate.Name);
        Assert.Equal(existing.Phone, candidate.Phone);
    }
}
