using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Common;
using HireFlow.Application.PublicApi;
using HireFlow.Domain.Screening;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Публичный API скрининга: сквозные сценарии POST /v1/screenings и вход по ключу клиента.
// Правила скоринга и валидации требований проверены в Application.Tests и Domain.Tests.
public class PublicScreeningApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    [Fact]
    public async Task Screenings_MultipartReturnsResultAndStoresNothingAboutCandidate()
    {
        var (name, client) = await App.NewClientAsync();
        var requirements = PublicApiHelpers.Requirements(["C#", "SQL"], ["Docker", "Kafka"], minYears: 3);
        var file = ResumeFactory.Pdf("Backend engineer", "5 years of experience", "C#, Docker");
        var before = await CountStoredDataAsync();

        var response = await client.PostAsync("/v1/screenings", PublicApiHelpers.Form(file, requirements.ToJson()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadAsync<PublicScreeningResponse>(response);
        // Обязательные 1 из 2 и желательные 1 из 2: 80 * 1/2 + 20 * 1/2 = 50, не хватает одного обязательного.
        Assert.Equal(50, result.Score);
        Assert.Equal(ScreeningVerdict.Borderline, result.Verdict);
        Assert.Equal(["C#"], result.MatchedRequiredSkills);
        Assert.Equal(["SQL"], result.MissingRequiredSkills);
        Assert.Equal(["Docker"], result.MatchedNiceToHaveSkills);
        Assert.Equal(["Kafka"], result.MissingNiceToHaveSkills);
        Assert.Equal(5, result.YearsOfExperience);
        Assert.Equal(YearsOfExperienceSource.Resume, result.YearsOfExperienceSource);
        Assert.True(result.ExperienceIsEnough);
        // ПД: ни кандидатов, ни откликов, ни загрузок резюме, ни файлов в хранилище.
        Assert.Equal(before, await CountStoredDataAsync());
        Assert.Equal(1, await App.UsageTodayAsync(name));
    }

    [Fact]
    public async Task Screenings_JsonTextReturnsResultWithYearsFromRequest()
    {
        var (_, client) = await App.NewClientAsync();

        var response = await client.PostAsJsonAsync("/v1/screenings", PublicApiHelpers.JsonBody("C# developer", years: 7), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadAsync<PublicScreeningResponse>(response);
        Assert.Equal(100, result.Score);
        Assert.Equal(ScreeningVerdict.Passed, result.Verdict);
        Assert.Equal(7, result.YearsOfExperience);
        Assert.Equal(YearsOfExperienceSource.Request, result.YearsOfExperienceSource);
        // В JSON перечисления в snake_case.
        Assert.Contains("\"yearsOfExperienceSource\":\"request\"", await response.Content.ReadAsStringAsync());
    }

    public enum Caller { Anonymous, WrongKey, RevokedKey, BotKeyAsBearer, BotKeyInXApiKey, RecruiterCookie }

    [Theory]
    [InlineData(Caller.Anonymous, "GET", "/v1/usage")]
    [InlineData(Caller.WrongKey, "GET", "/v1/usage")]
    [InlineData(Caller.RevokedKey, "GET", "/v1/usage")]
    [InlineData(Caller.BotKeyAsBearer, "GET", "/v1/usage")]
    [InlineData(Caller.BotKeyInXApiKey, "GET", "/v1/usage")]
    [InlineData(Caller.RecruiterCookie, "GET", "/v1/usage")]
    [InlineData(Caller.Anonymous, "POST", "/v1/screenings")]
    public async Task V1_WithoutValidClientKeyReturns401(Caller caller, string method, string route)
    {
        var (name, validClient) = await App.NewClientAsync();
        await App.RevokeAsync(name);
        using var client = caller switch
        {
            Caller.Anonymous => App.CreateClient(apiKey: null),
            Caller.WrongKey => App.CreateBearerClient("hf_" + new string('x', 43)),
            Caller.RevokedKey => validClient,
            Caller.BotKeyAsBearer => App.CreateBearerClient(ApiFactory.BotApiKey),
            Caller.BotKeyInXApiKey => App.CreateClient(ApiFactory.BotApiKey),
            _ => await App.CreateRecruiterClientAsync(),
        };

        var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (method == "POST")
            request.Content = JsonContent.Create(PublicApiHelpers.JsonBody("C#"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/applications")]
    [InlineData("/internal/notifications/due")]
    [InlineData("/vacancies")]
    [InlineData("/auth/me")]
    public async Task ClientKey_DoesNotOpenRecruiterOrBotRoutes(string route)
    {
        var (_, client) = await App.NewClientAsync();

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("requirements")]
    [InlineData("years")]
    [InlineData("jsonBody")]
    [InlineData("unknownCharset")]
    public async Task Screenings_MalformedInputReturns400(string broken)
    {
        var (_, client) = await App.NewClientAsync();
        var file = ResumeFactory.Pdf("C# developer");
        var validRequirements = PublicApiHelpers.Requirements().ToJson();
        using HttpContent content = broken switch
        {
            "requirements" => PublicApiHelpers.Form(file, "{not json"),
            "years" => PublicApiHelpers.Form(file, validRequirements, "many"),
            "unknownCharset" => UnknownCharsetJson(),
            _ => new StringContent("{not json", System.Text.Encoding.UTF8, "application/json"),
        };

        var response = await client.PostAsync("/v1/screenings", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IssueKey_StoresOnlyHashAndPrefixAndReissueInvalidatesOldKey()
    {
        var name = "client" + Guid.NewGuid().ToString("N")[..12];
        await Assert.ThrowsAsync<NotFoundException>(() => App.RevokeAsync(name));
        var oldKey = await App.IssueKeyAsync(name);

        await using (var scope = App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.ApiClients.AsNoTracking().SingleAsync(c => c.Name == name);
            Assert.Equal(ApiKeys.Hash(oldKey), row.KeyHash);
            Assert.Equal(oldKey[..8], row.KeyPrefix);
            // Открытого ключа нет ни в одном поле строки.
            Assert.DoesNotContain(oldKey, JsonSerializer.Serialize(row));
        }

        var newKey = await App.IssueKeyAsync(name);

        Assert.NotEqual(oldKey, newKey);
        using var oldClient = App.CreateBearerClient(oldKey);
        using var newClient = App.CreateBearerClient(newKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldClient.GetAsync("/v1/usage")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await newClient.GetAsync("/v1/usage")).StatusCode);
        await using var check = App.Services.CreateAsyncScope();
        Assert.Equal(1, await check.ServiceProvider.GetRequiredService<AppDbContext>().ApiClients.CountAsync(c => c.Name == name));
    }

    // Корректный JSON, но с неизвестной кодировкой в Content-Type: должен быть 400, а не 500.
    private static ByteArrayContent UnknownCharsetJson()
    {
        var content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(PublicApiHelpers.JsonBody("C# developer"), System.Text.Json.JsonSerializerOptions.Web)));
        content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=foo");
        return content;
    }

    // Считает кандидатов, отклики, загрузки резюме в БД и файлы в хранилище.
    private async Task<(int Candidates, int Applications, int Uploads, int Files)> CountStoredDataAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Candidates.CountAsync(), await db.JobApplications.CountAsync(),
            await db.ResumeUploads.CountAsync(), StoredFileCount());
    }
}
