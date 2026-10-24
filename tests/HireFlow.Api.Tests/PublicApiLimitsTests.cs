using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.PublicApi;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Дневная квота публичного API: квота нового клиента 3, у каждого теста свой клиент.
public class PublicApiQuotaTests(PublicApiQuotaFixture app) : IClassFixture<PublicApiQuotaFixture>
{
    [Fact]
    public async Task Screenings_ParallelRequestsOverQuotaGiveExactlyQuotaSuccessesAndRestAre429WithRetryAfter()
    {
        var (name, client) = await app.NewClientAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 2 * PublicApiQuotaFixture.Quota).Select(_ =>
            client.PostAsJsonAsync("/v1/screenings", PublicApiHelpers.JsonBody("C# developer"), ApiTestBase.Json)));

        Assert.Equal(PublicApiQuotaFixture.Quota, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        var limited = responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests).ToList();
        Assert.Equal(PublicApiQuotaFixture.Quota, limited.Count);
        foreach (var response in limited)
        {
            var seconds = int.Parse(Assert.Single(response.Headers.GetValues("Retry-After")), CultureInfo.InvariantCulture);
            Assert.InRange(seconds, 1, 24 * 60 * 60);
        }

        // /v1/usage сам квоту не списывает: два чтения подряд показывают одно и то же.
        for (var i = 0; i < 2; i++)
        {
            var usage = await (await client.GetAsync("/v1/usage")).Content.ReadFromJsonAsync<PublicUsageResponse>(ApiTestBase.Json);
            Assert.Equal(PublicApiQuotaFixture.Quota, usage!.RequestsToday);
            Assert.Equal(PublicApiQuotaFixture.Quota, usage.DailyQuota);
            Assert.Equal(DateTime.UtcNow.Date.AddDays(1), usage.ResetsAt);
        }
        Assert.Equal(PublicApiQuotaFixture.Quota, await app.UsageTodayAsync(name));
    }

    [Fact]
    public async Task Screenings_RejectedRequests400And413ChargeQuotaButExhaustedQuotaDoesNot()
    {
        var (name, client) = await app.NewClientAsync();
        var noRequirements = JsonContent.Create(new { text = "C# developer" });
        var bigFile = new byte[10 * 1024 * 1024 + 1];

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/v1/screenings", noRequirements)).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await client.PostAsync("/v1/screenings", PublicApiHelpers.Form(bigFile, PublicApiHelpers.Requirements().ToJson()))).StatusCode);
        Assert.Equal(2, await app.UsageTodayAsync(name));

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/v1/screenings", PublicApiHelpers.JsonBody("C# developer"), ApiTestBase.Json)).StatusCode);
        var exhausted = await client.PostAsJsonAsync("/v1/screenings", PublicApiHelpers.JsonBody("C# developer"), ApiTestBase.Json);

        Assert.Equal(HttpStatusCode.TooManyRequests, exhausted.StatusCode);
        Assert.Equal(PublicApiQuotaFixture.Quota, await app.UsageTodayAsync(name));
    }
}

// Лимит запросов в минуту: 2 на клиента.
public class PublicApiRateLimitTests(PublicApiRateLimitFixture app) : IClassFixture<PublicApiRateLimitFixture>
{
    [Fact]
    public async Task Screenings_RequestOverRateLimitReturns429WithRetryAfterDoesNotChargeQuotaAndDoesNotAffectOtherClient()
    {
        var (name, client) = await app.NewClientAsync();
        var (_, otherClient) = await app.NewClientAsync();
        var metrics = app.Services.GetRequiredService<HireFlow.Application.Common.HireFlowMetrics>();
        using var capture = new MetricsCapture(metrics.Meter);

        for (var i = 0; i < PublicApiRateLimitFixture.Limit; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/screenings", PublicApiHelpers.JsonBody("C#"))).StatusCode);
        var limited = await client.PostAsJsonAsync("/v1/screenings", PublicApiHelpers.JsonBody("C#"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        var seconds = int.Parse(Assert.Single(limited.Headers.GetValues("Retry-After")), CultureInfo.InvariantCulture);
        Assert.InRange(seconds, 1, 60);
        // Отказ лимитера квоту не списал; чужой клиент со своим окном проходит; метрика входа не растёт.
        Assert.Equal(PublicApiRateLimitFixture.Limit, await app.UsageTodayAsync(name));
        Assert.Equal(HttpStatusCode.OK, (await otherClient.PostAsJsonAsync("/v1/screenings", PublicApiHelpers.JsonBody("C#"))).StatusCode);
        Assert.Empty(capture.Of("hireflow.recruiter.logins"));
    }
}
