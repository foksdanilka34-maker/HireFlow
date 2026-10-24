using System.Net;
using System.Net.Http.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Common;
using HireFlow.Application.Recruiters;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Лимит попыток входа: отдельный класс со своим приложением, чтобы исчерпанный лимит не мешал другим тестам входа.
public class LoginRateLimitTests(LoginLimitFixture app) : IClassFixture<LoginLimitFixture>
{
    [Fact]
    public async Task Login_AttemptOverLimitReturns429EvenWithCorrectPasswordAndIssuesNoSession()
    {
        using var client = app.CreateClient(apiKey: null);
        using var capture = new MetricsCapture(app.Services.GetRequiredService<HireFlowMetrics>().Meter);
        var wrong = new LoginRequest { Login = ApiAppFixture.RecruiterLogin, Password = "wrong-password" };

        for (var i = 0; i < LoginLimitFixture.Limit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/login", wrong)).StatusCode);

        var correct = new LoginRequest { Login = ApiAppFixture.RecruiterLogin, Password = ApiAppFixture.RecruiterPassword };
        var overLimit = await client.PostAsJsonAsync("/auth/login", correct);

        Assert.Equal(HttpStatusCode.TooManyRequests, overLimit.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
        // Отказ лимита пишет ровно одно измерение входа с result=limited (неудачные попытки пишутся как failure).
        var limited = Assert.Single(capture.Of("hireflow.recruiter.logins"), s => (string?)s.Tags["result"] == "limited");
        Assert.Equal(1, limited.Value);
    }
}
