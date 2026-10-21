using System.Net;
using HireFlow.Api.Tests.Infrastructure;

namespace HireFlow.Api.Tests;

// Матрица доступа: кто куда может ходить. Маршруты рекрутера закрыты cookie, маршруты бота — ключом.
public class AccessMatrixTests(ApiAppFixture app) : ApiTestBase(app)
{
    public enum Caller { Anonymous, KeyOnly, CookieOnly }

    private const string Vacancy = "{vacancy}";

    // Строки матрицы: метод, маршрут, кто вызывает, ожидаемый код.
    public static TheoryData<string, string, Caller, HttpStatusCode> Cases()
    {
        var data = new TheoryData<string, string, Caller, HttpStatusCode>();
        var resume = $"/applications/{Guid.NewGuid()}/resume";
        var resumeText = $"{resume}/text";

        // Маршруты рекрутера: без входа и только с ключом бота — 401, с сессией работают.
        foreach (var (method, route) in new[] { ("GET", "/applications"), ("POST", "/vacancies"), ("GET", resume), ("GET", resumeText), ("GET", "/auth/me") })
        {
            data.Add(method, route, Caller.Anonymous, HttpStatusCode.Unauthorized);
            data.Add(method, route, Caller.KeyOnly, HttpStatusCode.Unauthorized);
        }
        data.Add("GET", "/applications", Caller.CookieOnly, HttpStatusCode.OK);
        data.Add("GET", "/auth/me", Caller.CookieOnly, HttpStatusCode.OK);

        // Маршруты бота: рекрутер с сессией, но без ключа — 401.
        foreach (var (method, route) in new[]
                 {
                     ("GET", "/candidates/1"), ("GET", "/bot-sessions/1"), ("GET", "/internal/notifications/due"),
                     ("POST", "/applications"),
                 })
            data.Add(method, route, Caller.CookieOnly, HttpStatusCode.Unauthorized);
        data.Add("GET", "/internal/notifications/due", Caller.KeyOnly, HttpStatusCode.OK);

        // Вакансии на чтение доступны и рекрутеру, и боту.
        foreach (var route in new[] { "/vacancies", $"/vacancies/{Vacancy}" })
        {
            data.Add("GET", route, Caller.Anonymous, HttpStatusCode.Unauthorized);
            data.Add("GET", route, Caller.KeyOnly, HttpStatusCode.OK);
            data.Add("GET", route, Caller.CookieOnly, HttpStatusCode.OK);
        }

        // Открытые маршруты.
        data.Add("GET", "/health", Caller.Anonymous, HttpStatusCode.OK);
        data.Add("GET", "/", Caller.Anonymous, HttpStatusCode.OK);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Route_ReturnsExpectedStatusForCaller(string method, string route, Caller caller, HttpStatusCode expected)
    {
        var vacancy = await CreateVacancyAsync([Unique("s")]);
        using var client = caller switch
        {
            Caller.Anonymous => App.CreateClient(apiKey: null),
            Caller.KeyOnly => App.CreateClient(ApiFactory.BotApiKey),
            _ => await App.CreateRecruiterClientAsync(),
        };

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route.Replace(Vacancy, vacancy.Id.ToString())));

        Assert.Equal(expected, response.StatusCode);
    }
}
