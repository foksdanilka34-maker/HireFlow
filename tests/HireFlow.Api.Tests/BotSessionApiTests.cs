using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.BotSessions;

namespace HireFlow.Api.Tests;

public class BotSessionApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    private Task<HttpResponseMessage> PutRawAsync(long telegramId, string json) =>
        Client.PutAsync($"/bot-sessions/{telegramId}", new StringContent(json, Encoding.UTF8, "application/json"));

    [Fact]
    public async Task Get_UnknownSessionReturns404()
    {
        var response = await Client.GetAsync($"/bot-sessions/{NewTelegramId()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_ThenGetReturnsSameStepVacancyAndDraft()
    {
        var telegramId = NewTelegramId();
        var vacancy = await CreateVacancyAsync([Unique("s")]);
        var draft = $$"""
            {"resume_file_id":"AgACAgIA","resume_file_name":"Резюме Иван.pdf","years_of_experience":5,
             "phone_found":true,"vacancy_ids":["{{vacancy.Id}}"],"page":1}
            """;

        var put = await PutRawAsync(telegramId, $$"""{"step":"choosing_vacancy","linkedVacancyId":"{{vacancy.Id}}","draft":{{draft}}}""");
        var get = await Client.GetAsync($"/bot-sessions/{telegramId}");

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var session = await ReadAsync<BotSessionResponse>(get);
        Assert.Equal(telegramId, session.TelegramId);
        Assert.Equal("choosing_vacancy", session.Step);
        Assert.Equal(vacancy.Id, session.LinkedVacancyId);
        Assert.Equal("AgACAgIA", session.Draft.GetProperty("resume_file_id").GetString());
        Assert.Equal("Резюме Иван.pdf", session.Draft.GetProperty("resume_file_name").GetString());
        Assert.Equal(5, session.Draft.GetProperty("years_of_experience").GetInt32());
        Assert.True(session.Draft.GetProperty("phone_found").GetBoolean());
        Assert.Equal(1, session.Draft.GetProperty("page").GetInt32());
        Assert.Equal(vacancy.Id, session.Draft.GetProperty("vacancy_ids")[0].GetGuid());
    }

    [Fact]
    public async Task Put_SecondTimeReplacesStateOfTheSameSession()
    {
        var telegramId = NewTelegramId();
        (await PutRawAsync(telegramId, """{"step":"awaiting_experience","linkedVacancyId":null,"draft":{"page":0}}""")).EnsureSuccessStatusCode();

        (await PutRawAsync(telegramId, """{"step":"awaiting_phone","linkedVacancyId":null,"draft":{"page":2}}""")).EnsureSuccessStatusCode();

        var session = await ReadAsync<BotSessionResponse>(await Client.GetAsync($"/bot-sessions/{telegramId}"));
        Assert.Equal("awaiting_phone", session.Step);
        Assert.Equal(2, session.Draft.GetProperty("page").GetInt32());
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("null")]
    public async Task Put_DraftThatIsNotAnObjectReturns400AndSavesNothing(string draft)
    {
        var telegramId = NewTelegramId();

        var response = await PutRawAsync(telegramId, $$"""{"step":"awaiting_resume","draft":{{draft}}}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/bot-sessions/{telegramId}")).StatusCode);
    }
}
