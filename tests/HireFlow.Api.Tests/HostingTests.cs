using System.Net;
using System.Net.Http.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Recruiters;

namespace HireFlow.Api.Tests;

// Настройки размещения API (Hosting:*): отдельные приложения, чтобы исчерпанный лимит не мешал другим тестам.
public class TrustedForwardedHeadersTests(TrustedProxyLoginFixture app) : IClassFixture<TrustedProxyLoginFixture>
{
    [Fact]
    public async Task Login_TrustForwardedHeadersOn_EachForwardedAddressHasOwnLimit()
    {
        using var client = app.CreateClient(apiKey: null);

        for (var i = 0; i < LoginLimitFixture.Limit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginWrongAsync(client, "203.0.113.10"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginWrongAsync(client, "203.0.113.10"));

        Assert.Equal(HttpStatusCode.Unauthorized, await LoginWrongAsync(client, "203.0.113.20"));
    }

    // За TLS-прокси схема берётся из X-Forwarded-Proto: cookie сессии получает Secure.
    [Fact]
    public async Task Login_TrustForwardedHeadersOnAndForwardedHttps_SessionCookieIsSecure()
    {
        using var client = app.CreateClient(apiKey: null);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest { Login = ApiAppFixture.RecruiterLogin, Password = ApiAppFixture.RecruiterPassword })
        };
        request.Headers.Add("X-Forwarded-For", "203.0.113.30");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("hireflow.auth="));
        var attributes = cookie.Split(';').Select(a => a.Trim());
        Assert.Contains("secure", attributes, StringComparer.OrdinalIgnoreCase);
    }

    // Отправляет неверный вход с заданным X-Forwarded-For и возвращает код ответа.
    internal static async Task<HttpStatusCode> LoginWrongAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest { Login = ApiAppFixture.RecruiterLogin, Password = "wrong-password" })
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}

public class UntrustedForwardedHeadersTests(LoginLimitFixture app) : IClassFixture<LoginLimitFixture>
{
    [Fact]
    public async Task Login_TrustForwardedHeadersOffByDefault_SpoofedHeaderDoesNotBypassLimit()
    {
        using var client = app.CreateClient(apiKey: null);

        for (var i = 0; i < LoginLimitFixture.Limit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await TrustedForwardedHeadersTests.LoginWrongAsync(client, "203.0.113.10"));

        Assert.Equal(HttpStatusCode.TooManyRequests, await TrustedForwardedHeadersTests.LoginWrongAsync(client, "203.0.113.20"));
    }
}

public class DataProtectionKeysPathTests(DataProtectionKeysFixture app) : IClassFixture<DataProtectionKeysFixture>
{
    [Fact]
    public void Login_KeysPathConfigured_KeyFileIsWrittenToThatFolder()
    {
        // Фикстура уже вошла рекрутером, значит cookie зашифрована и ключ создан.
        Assert.True(Directory.Exists(app.KeysPath));
        Assert.NotEmpty(Directory.GetFiles(app.KeysPath, "*.xml"));
    }
}
