using System.Net;
using System.Net.Http.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Recruiters;

namespace HireFlow.Api.Tests;

// Лимит попыток входа: отдельный класс со своим приложением, чтобы исчерпанный лимит не мешал другим тестам входа.
public class LoginRateLimitTests(LoginLimitFixture app) : IClassFixture<LoginLimitFixture>
{
    [Fact]
    public async Task Login_AttemptOverLimitReturns429EvenWithCorrectPasswordAndIssuesNoSession()
    {
        using var client = app.CreateClient(apiKey: null);
        var wrong = new LoginRequest { Login = ApiAppFixture.RecruiterLogin, Password = "wrong-password" };

        for (var i = 0; i < LoginLimitFixture.Limit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/login", wrong)).StatusCode);

        var correct = new LoginRequest { Login = ApiAppFixture.RecruiterLogin, Password = ApiAppFixture.RecruiterPassword };
        var overLimit = await client.PostAsJsonAsync("/auth/login", correct);

        Assert.Equal(HttpStatusCode.TooManyRequests, overLimit.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
    }
}
