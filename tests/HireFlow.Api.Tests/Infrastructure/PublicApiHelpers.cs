using System.Net.Http.Headers;
using System.Text.Json;
using HireFlow.Application.PublicApi;
using HireFlow.Domain.Vacancies;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests.Infrastructure;

// Помощники тестов публичного API: клиенты и ключи через ApiClientService, запросы /v1.
public static class PublicApiHelpers
{
    // Выдаёт ключ клиенту (создаёт его или заменяет ключ), как это делает консольная команда.
    public static async Task<string> IssueKeyAsync(this ApiAppFixture app, string name)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IApiClientService>();
        return (await service.IssueKeyAsync(name, CancellationToken.None)).Key;
    }

    // Отзывает ключ клиента через сервис.
    public static async Task RevokeAsync(this ApiAppFixture app, string name)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IApiClientService>().RevokeAsync(name, CancellationToken.None);
    }

    // Создаёт клиент без ключа бота и cookie, с Authorization: Bearer <key>.
    public static HttpClient CreateBearerClient(this ApiAppFixture app, string key)
    {
        var client = app.CreateClient(apiKey: null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    // Выдаёт новому клиенту ключ и возвращает имя и HTTP-клиент с этим ключом.
    public static async Task<(string Name, HttpClient Client)> NewClientAsync(this ApiAppFixture app)
    {
        var name = "client" + Guid.NewGuid().ToString("N")[..12];
        return (name, app.CreateBearerClient(await app.IssueKeyAsync(name)));
    }

    // Читает счётчик запросов клиента за сегодня (UTC) прямо из БД, минуя лимитер и квоту.
    public static async Task<int> UsageTodayAsync(this ApiAppFixture app, string clientName)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = await db.ApiClients.Where(c => c.Name == clientName).Select(c => c.Id).SingleAsync();
        return await scope.ServiceProvider.GetRequiredService<IApiUsageCounter>()
            .GetAsync(id, DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);
    }

    // Требования с одним обязательным навыком: проходит любой текст, где он есть.
    public static VacancyRequirements Requirements(string[]? required = null, string[]? niceToHave = null, int minYears = 0) => new()
    {
        RequiredSkills = (required ?? ["C#"]).Select(n => new Skill { Name = n }).ToList(),
        NiceToHaveSkills = (niceToHave ?? []).Select(n => new Skill { Name = n }).ToList(),
        MinYearsOfExperience = minYears,
        PassingScore = 70,
        BorderlineWidth = 10,
    };

    // Тело JSON-варианта POST /v1/screenings.
    public static object JsonBody(string text, VacancyRequirements? requirements = null, int? years = null) =>
        new { text, requirements = requirements ?? Requirements(), yearsOfExperience = years };

    // Multipart-форма POST /v1/screenings; строковые поля передаются как есть.
    public static MultipartFormDataContent Form(byte[] file, string? requirementsJson, string? years = null)
    {
        var form = new MultipartFormDataContent();
        if (requirementsJson is not null)
            form.Add(new StringContent(requirementsJson), "requirements");
        if (years is not null)
            form.Add(new StringContent(years), "yearsOfExperience");
        form.Add(new ByteArrayContent(file), "file", "resume.pdf");
        return form;
    }

    // Сериализует требования так, как их шлёт клиент: camelCase.
    public static string ToJson(this VacancyRequirements requirements) =>
        JsonSerializer.Serialize(requirements, JsonSerializerOptions.Web);
}
