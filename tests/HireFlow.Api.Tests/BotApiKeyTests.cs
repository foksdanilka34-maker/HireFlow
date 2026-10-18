using System.Net;
using HireFlow.Api.Tests.Infrastructure;

namespace HireFlow.Api.Tests;

// Служебный ключ бота: маршруты бота закрыты X-Api-Key, маршруты рекрутера пока открыты.
public class BotApiKeyTests(ApiAppFixture app) : ApiTestBase(app)
{
    public static TheoryData<string> BotGetRoutes() => new()
    {
        "/candidates/1",
        "/candidates/1/applications",
        "/bot-sessions/1",
        "/internal/notifications/due",
        "/vacancies",
        $"/vacancies/{Guid.NewGuid()}",
    };

    [Theory]
    [MemberData(nameof(BotGetRoutes))]
    public async Task BotRoute_WithoutKeyReturns401(string route)
    {
        using var client = App.CreateClient(apiKey: null);

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(BotGetRoutes))]
    public async Task BotRoute_WithWrongKeyReturns401(string route)
    {
        using var client = App.CreateClient("wrong-key");

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BotRoute_WithWrongKeyOfSameLengthReturns401()
    {
        using var client = App.CreateClient(new string('x', ApiFactory.BotApiKey.Length));

        var response = await client.GetAsync("/bot-sessions/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task CreateApplication_WithoutValidKeyReturns401AndCreatesNothing(string? key)
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var telegramId = NewTelegramId();
        using var client = App.CreateClient(key);
        var form = ResumeForm(
            ResumeWith(skill), "resume.pdf", 5,
            ("telegramId", telegramId.ToString()), ("vacancyId", vacancy.Id.ToString()),
            ("name", "Иван"), ("phone", "+79991234567"));

        var response = await client.PostAsync("/applications", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountApplicationsAsync(vacancy.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/candidates/{telegramId}")).StatusCode);
    }

    [Fact]
    public async Task RecruiterRoute_WithoutKeyIsNotRejectedAs401()
    {
        using var client = App.CreateClient(apiKey: null);

        var response = await client.GetAsync("/applications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
