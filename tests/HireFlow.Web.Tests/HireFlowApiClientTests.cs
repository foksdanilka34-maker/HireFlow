using System.Net;
using HireFlow.Web.Api;
using HireFlow.Web.Auth;

namespace HireFlow.Web.Tests;

public class HireFlowApiClientTests
{
    private static readonly RecruiterDto Recruiter = new(Guid.NewGuid(), "ivanov");
    private static readonly VacancyRequestDto VacancyRequest = new("Title", "QA", null, new RequirementsDto([], [], 0, 70, 10), 90);

    // Собирает клиент на фейковом обработчике; рекрутер уже вошёл.
    private static (HireFlowApiClient Client, AuthState Auth) Create(FakeHandler handler)
    {
        var auth = new AuthState();
        auth.SignIn(Recruiter);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5184/") };
        return (new HireFlowApiClient(http, auth), auth);
    }

    // Приводит результат со значением к результату без значения.
    private static async Task<ApiResult> Plain<T>(Task<ApiResult<T>> call)
    {
        var result = await call;
        return new ApiResult(result.Status, result.Error);
    }

    // Все запросы клиента, кроме входа: для каждого действует одно правило про 401 и сеть.
    public static TheoryData<string, Func<HireFlowApiClient, Task<ApiResult>>> Calls() => new()
    {
        { "me", c => Plain(c.GetMeAsync()) },
        { "list", c => Plain(c.GetVacanciesAsync()) },
        { "get", c => Plain(c.GetVacancyAsync(Guid.NewGuid())) },
        { "create", c => Plain(c.CreateVacancyAsync(VacancyRequest)) },
        { "update", c => Plain(c.UpdateVacancyAsync(Guid.NewGuid(), VacancyRequest)) },
        { "close", c => Plain(c.CloseAsync(Guid.NewGuid())) },
        { "delete", c => c.DeleteVacancyAsync(Guid.NewGuid()) },
        { "logout", c => c.LogoutAsync() },
    };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Unauthorized_SignsOutAndReturnsError(string name, Func<HireFlowApiClient, Task<ApiResult>> call)
    {
        var (client, auth) = Create(FakeHandler.Returns(HttpStatusCode.Unauthorized));

        var result = await call(client);

        Assert.False(auth.IsSignedIn, name);
        Assert.Equal(401, result.Status);
        Assert.Equal(HireFlowApiClient.DefaultError(401), result.Error);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task NetworkError_ReturnsUnavailableResultInsteadOfThrowingAndKeepsSession(
        string name, Func<HireFlowApiClient, Task<ApiResult>> call)
    {
        var (client, auth) = Create(FakeHandler.NetworkFails());

        var result = await call(client);

        Assert.Equal(0, result.Status);
        Assert.Equal(HireFlowApiClient.UnavailableError, result.Error);
        Assert.False(result.IsOk);
        // Недоступный API не означает конец сессии, кроме выхода, который сбрасывает её всегда.
        Assert.Equal(name == "logout", !auth.IsSignedIn);
    }

    // Не только обрыв сети: таймаут HttpClient и успешный ответ не от API (например, HTML от DevServer при неверном адресе).
    [Theory]
    [InlineData("timeout")]
    [InlineData("html200")]
    public async Task OtherUnavailableCases_ReturnUnavailableResultInsteadOfThrowing(string kind)
    {
        var handler = kind == "timeout"
            ? FakeHandler.Throws(new TaskCanceledException("timeout"))
            : FakeHandler.Returns(HttpStatusCode.OK, "<html>index</html>", "text/html");
        var (client, _) = Create(handler);

        var result = await client.GetVacanciesAsync();

        Assert.Equal(0, result.Status);
        Assert.Equal(HireFlowApiClient.UnavailableError, result.Error);
    }

    // Отмену, запрошенную вызывающим, нельзя превращать в «API недоступен».
    [Fact]
    public async Task Cancellation_RequestedByCallerIsPropagated()
    {
        var (client, _) = Create(FakeHandler.Throws(new TaskCanceledException()));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetVacanciesAsync(ct: cts.Token));
    }

    [Fact]
    public async Task Login_401ShowsInvalidCredentialsAndDoesNotTouchSession()
    {
        var (client, auth) = Create(FakeHandler.Returns(HttpStatusCode.Unauthorized, """{"detail":"Invalid login or password."}"""));

        var result = await client.LoginAsync("ivanov", "wrong");

        Assert.Equal(401, result.Status);
        Assert.Equal(HireFlowApiClient.InvalidCredentialsError, result.Error);
        Assert.True(auth.IsSignedIn);
        Assert.Equal(Recruiter, auth.Recruiter);
    }

    [Fact]
    public async Task Login_429ShowsTooManyAttempts()
    {
        var (client, _) = Create(FakeHandler.Returns(HttpStatusCode.TooManyRequests));

        var result = await client.LoginAsync("ivanov", "x");

        Assert.Equal(429, result.Status);
        Assert.Equal(HireFlowApiClient.TooManyAttemptsError, result.Error);
    }

    [Fact]
    public async Task Login_SuccessSignsInReturnedRecruiter()
    {
        var other = new RecruiterDto(Guid.NewGuid(), "petrov");
        var (client, auth) = Create(FakeHandler.Returns(HttpStatusCode.OK, $$"""{"id":"{{other.Id}}","login":"petrov"}"""));

        var result = await client.LoginAsync("petrov", "pw");

        Assert.True(result.IsOk);
        Assert.Equal(other, auth.Recruiter);
    }

    // Текст ошибки из тела ответа; null в ожидаемом значении означает общий текст по коду.
    [Theory]
    [InlineData(400, """{"errors":{"Title":["Required"],"Direction":["Too long","Bad"]}}""", "Title: Required; Direction: Too long; Direction: Bad")]
    [InlineData(400, """{"errors":{},"detail":"Bad request body"}""", "Bad request body")]
    [InlineData(409, """{"status":409,"detail":"Vacancy is archived."}""", "Vacancy is archived.")]
    [InlineData(409, null, null)]
    [InlineData(404, "{}", null)]
    [InlineData(409, """{"detail":"  "}""", null)]
    public async Task Error_IsBuiltFromProblemDetailsOrDefaultByStatus(int status, string? body, string? expected)
    {
        var (client, auth) = Create(FakeHandler.Returns((HttpStatusCode)status, body));

        var result = await client.OpenAsync(Guid.NewGuid());

        Assert.Equal(status, result.Status);
        Assert.Equal(expected ?? HireFlowApiClient.DefaultError(status), result.Error);
        Assert.True(auth.IsSignedIn);
    }

    // Прокси или балансировщик может вернуть не JSON: страница не должна падать.
    [Theory]
    [InlineData(502, "<html>Bad Gateway</html>", "text/html")]
    [InlineData(500, "not json", "application/json")]
    public async Task Error_NonJsonBodyFallsBackToDefaultText(int status, string body, string mediaType)
    {
        var (client, _) = Create(FakeHandler.Returns((HttpStatusCode)status, body, mediaType));

        var result = await client.GetVacanciesAsync();

        Assert.Equal(status, result.Status);
        Assert.Equal(HireFlowApiClient.DefaultError(status), result.Error);
    }

    [Theory]
    [InlineData(null, "applications")]
    [InlineData(ApplicationStatus.RecruiterReview, "applications?status=recruiter_review")]
    [InlineData(ApplicationStatus.AutoRejected, "applications?status=auto_rejected")]
    public async Task GetApplications_BuildsSnakeCaseStatusQuery(ApplicationStatus? status, string expected)
    {
        var handler = FakeHandler.Returns(HttpStatusCode.OK, "[]");
        var (client, _) = Create(handler);

        var result = await client.GetApplicationsAsync(status);

        Assert.True(result.IsOk);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("http://localhost:5184/" + expected, handler.LastUrl);
    }

    [Theory]
    [InlineData(RecruiterDecision.Invite, "invite")]
    [InlineData(RecruiterDecision.Reject, "reject")]
    public async Task Decide_PostsSnakeCaseDecisionAndComment(RecruiterDecision decision, string expected)
    {
        var id = Guid.NewGuid();
        var handler = FakeHandler.Returns(HttpStatusCode.Conflict);
        var (client, _) = Create(handler);

        await client.DecideAsync(id, new DecisionRequestDto(decision, "ok"));

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"http://localhost:5184/applications/{id}/decision", handler.LastUrl);
        using var body = System.Text.Json.JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(expected, body.RootElement.GetProperty("decision").GetString());
        Assert.Equal("ok", body.RootElement.GetProperty("comment").GetString());
    }

    // Адрес для iframe не должен зависеть от того, как записан BaseAddress.
    [Theory]
    [InlineData("http://localhost:5184/", false, "http://localhost:5184/applications/{0}/resume")]
    [InlineData("http://localhost:5184", false, "http://localhost:5184/applications/{0}/resume")]
    [InlineData("http://localhost:5184/", true, "http://localhost:5184/applications/{0}/resume?inline=true")]
    [InlineData("http://localhost:5184", true, "http://localhost:5184/applications/{0}/resume?inline=true")]
    public void ResumeUrl_BuildsAddressFromBaseAddressWithOrWithoutTrailingSlash(string baseAddress, bool inline, string expected)
    {
        var id = Guid.NewGuid();
        var http = new HttpClient(FakeHandler.Returns(HttpStatusCode.OK)) { BaseAddress = new Uri(baseAddress) };
        var client = new HireFlowApiClient(http, new AuthState());

        Assert.Equal(string.Format(expected, id), client.ResumeUrl(id, inline));
    }
}
