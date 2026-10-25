using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Публичная документация API, заголовок 401 и метрика длительности запросов /v1.
// Правила скрининга и доступа по ключу проверены в PublicScreeningApiTests.
public class PublicApiDocsTests(ApiAppFixture app) : ApiTestBase(app)
{
    private const string DurationMetric = "hireflow.public_api.request.duration";

    [Fact]
    public async Task OpenApi_IsOpenWithoutKeyAndDescribesOnlyPublicRoutes()
    {
        using var anonymous = App.CreateClient(apiKey: null);

        var response = await anonymous.GetAsync("/v1/openapi.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        // Ровно два публичных маршрута: ни /vacancies, ни /internal, ни /auth в документ не попали.
        Assert.Equal(["/v1/screenings", "/v1/usage"], root.GetProperty("paths").EnumerateObject().Select(p => p.Name).Order());
        var bearer = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task Docs_OpenWithoutKeyAndPointToPublicDocument()
    {
        using var anonymous = App.CreateClient(apiKey: null);

        var response = await anonymous.GetAsync("/v1/docs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        // Scalar берёт адрес документа относительно страницы, поэтому проверяем путь без ведущего слэша.
        Assert.Contains("\"url\":\"v1/openapi.json\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task V1_Unauthorized401AsksForBearer()
    {
        using var anonymous = App.CreateClient(apiKey: null);

        var response = await anonymous.GetAsync("/v1/usage");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).ToString());
    }

    [Fact]
    public async Task Metric_ClientScreeningRecordsOneMeasurementWithClientRouteStatusAndSize()
    {
        var (name, client) = await App.NewClientAsync();
        using var capture = new MetricsCapture(App.Services.GetRequiredService<HireFlowMetrics>().Meter);

        var response = await client.PostAsJsonAsync("/v1/screenings", PublicApiHelpers.JsonBody("C# developer"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sample = Assert.Single(capture.Of(DurationMetric));
        Assert.Equal(["client", "http_route", "size", "status_code"], sample.Tags.Keys.Order());
        Assert.Equal(name, sample.Tags["client"]);
        Assert.Equal("/v1/screenings", sample.Tags["http_route"]);
        Assert.Equal(200, sample.Tags["status_code"]);
        Assert.Equal("small", sample.Tags["size"]);
        Assert.True(sample.Value > 0);
    }

    [Fact]
    public async Task Metric_RequestWithoutKeyRecordsAnonymousWithFinal401()
    {
        using var anonymous = App.CreateClient(apiKey: null);
        using var capture = new MetricsCapture(App.Services.GetRequiredService<HireFlowMetrics>().Meter);

        var response = await anonymous.GetAsync("/v1/usage");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var sample = Assert.Single(capture.Of(DurationMetric));
        Assert.Equal("anonymous", sample.Tags["client"]);
        Assert.Equal("/v1/usage", sample.Tags["http_route"]);
        Assert.Equal(401, sample.Tags["status_code"]);
    }
}
