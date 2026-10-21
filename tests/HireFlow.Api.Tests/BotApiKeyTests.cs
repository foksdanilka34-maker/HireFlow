using System.Net;
using HireFlow.Api.Tests.Infrastructure;

namespace HireFlow.Api.Tests;

// Служебный ключ бота: маршруты бота закрыты X-Api-Key (доступ рекрутера к маршрутам — в AccessMatrixTests).
public class BotApiKeyTests(ApiAppFixture app) : ApiTestBase(app)
{
    private static readonly string[] BotGetRoutes =
    [
        "/candidates/1",
        "/candidates/1/applications",
        "/bot-sessions/1",
        "/internal/notifications/due",
        "/vacancies",
        $"/vacancies/{Guid.NewGuid()}",
    ];

    // Маршрут бота × ключ: без заголовка и с неверным ключом той же длины.
    public static TheoryData<string, string?> RoutesAndInvalidKeys()
    {
        var data = new TheoryData<string, string?>();
        foreach (var route in BotGetRoutes)
        {
            data.Add(route, null);
            data.Add(route, new string('x', ApiFactory.BotApiKey.Length));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RoutesAndInvalidKeys))]
    public async Task BotRoute_WithoutOrWithWrongKeyReturns401(string route, string? key)
    {
        using var client = App.CreateClient(key);

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateApplication_WithoutKeyReturns401()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        using var client = App.CreateClient(apiKey: null);
        var form = ResumeForm(
            ResumeWith(skill), "resume.pdf", 5,
            ("telegramId", NewTelegramId().ToString()), ("vacancyId", vacancy.Id.ToString()),
            ("name", "Иван"), ("phone", "+79991234567"));

        var response = await client.PostAsync("/applications", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
