using System.Net;
using HireFlow.Api.Tests.Infrastructure;

namespace HireFlow.Api.Tests;

// CORS для кабинета рекрутера: разрешён только адрес из Web:Origins, cookie передаются.
public class CorsApiTests(ApiAppFixture app) : IClassFixture<ApiAppFixture>
{
    // Отправляет preflight-запрос на /vacancies с заданным Origin.
    private async Task<HttpResponseMessage> PreflightAsync(string origin)
    {
        using var client = app.CreateClient(apiKey: null);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/vacancies");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Preflight_AllowedOriginGetsOriginAndCredentialsHeaders()
    {
        var response = await PreflightAsync(ApiFactory.WebOrigin);

        Assert.True(response.IsSuccessStatusCode, $"preflight вернул {(int)response.StatusCode}");
        Assert.Equal(ApiFactory.WebOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    // Чужой сайт, другой порт и другая схема не получают разрешающих заголовков.
    [Theory]
    [InlineData("http://evil.example")]
    [InlineData("http://localhost:5201")]
    [InlineData("https://localhost:5200")]
    public async Task Preflight_ForeignOriginGetsNoCorsHeaders(string origin)
    {
        var response = await PreflightAsync(origin);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Get_AllowedOriginWithCookieReturns200WithCorsHeaders()
    {
        using var client = await app.CreateRecruiterClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Add("Origin", ApiFactory.WebOrigin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ApiFactory.WebOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }
}
