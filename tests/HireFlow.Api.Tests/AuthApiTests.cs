using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Common;
using HireFlow.Application.Recruiters;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Вход, выход и пароли рекрутеров. Клиенты здесь без ключа бота: проверяется только сессия.
public class AuthApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    // Создаёт рекрутера с уникальным логином и возвращает логин.
    private async Task<string> NewRecruiterAsync(string password)
    {
        var login = "r-" + Guid.NewGuid().ToString("N")[..12];
        await App.CreateRecruiterAsync(login, password);
        return login;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string login, string password) =>
        client.PostAsJsonAsync("/auth/login", new LoginRequest { Login = login, Password = password });

    [Theory]
    [InlineData("{0}")]
    [InlineData("  {0}  ")]
    public async Task Login_CorrectPasswordReturnsCookieAndMeShowsLogin(string loginFormat)
    {
        var login = await NewRecruiterAsync("password-123");
        // Заглавные буквы и пробелы по краям: логин нормализуется, как при создании.
        var typed = string.Format(loginFormat, login.ToUpperInvariant());
        using var client = App.CreateClient(apiKey: null);

        var response = await LoginAsync(client, typed, "password-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("hireflow.auth="));
        Assert.Equal(login, (await ReadAsync<RecruiterResponse>(response)).Login);
        var me = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(login, (await ReadAsync<RecruiterResponse>(me)).Login);
    }

    // Неверный пароль, незнакомый логин и логин недопустимого вида неотличимы: один статус и одно сообщение.
    [Theory]
    [InlineData(ApiAppFixture.RecruiterLogin, "wrong-password")]
    [InlineData("nobody-here", ApiAppFixture.RecruiterPassword)]
    [InlineData("Нет Такого", ApiAppFixture.RecruiterPassword)]
    public async Task Login_WrongPasswordOrUnknownLoginReturnsSame401(string login, string password)
    {
        using var client = App.CreateClient(apiKey: null);

        var response = await LoginAsync(client, login, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Invalid login or password.", body.RootElement.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        using var client = await App.CreateRecruiterClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/me")).StatusCode);

        var response = await client.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task SetPassword_ForExistingLoginChangesPassword()
    {
        var login = await NewRecruiterAsync("old-password");
        using var client = App.CreateClient(apiKey: null);

        // Ровно 8 символов — допустимая граница.
        await App.CreateRecruiterAsync(login, "12345678");

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, login, "old-password")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, login, "12345678")).StatusCode);
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Recruiters.CountAsync(r => r.Login == login));
    }

    [Fact]
    public async Task SetPassword_SevenCharactersIsRejectedAndNothingIsCreated()
    {
        var login = "r-" + Guid.NewGuid().ToString("N")[..12];

        await Assert.ThrowsAsync<BadRequestException>(() => App.CreateRecruiterAsync(login, "1234567"));

        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Recruiters.AnyAsync(r => r.Login == login));
    }
}
