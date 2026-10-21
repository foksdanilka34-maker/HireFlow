using System.Net.Http.Json;
using HireFlow.Application.Recruiters;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests.Infrastructure;

// Фикстура тестового класса: своя база с применёнными миграциями, своё приложение и своя папка хранилища.
public class ApiAppFixture : IAsyncLifetime
{
    private ApiFactory? _factory;

    // Рекрутер фикстуры: основной Client уже вошёл под ним.
    public const string RecruiterLogin = "fixture-recruiter";
    public const string RecruiterPassword = "fixture-password";

    // Id рекрутера фикстуры: для тестов, которые вызывают доменные методы решений напрямую (FK на recruiters).
    public Guid RecruiterId { get; private set; }

    public string ConnectionString { get; private set; } = null!;
    public string StoragePath { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _factory!.Services;

    // Лимит попыток входа на этом приложении; тесты лимита переопределяют его маленьким значением.
    protected virtual int LoginAttemptsPerMinute => ApiFactory.DefaultLoginAttempts;

    // Входит ли основной Client при старте (тест лимита отключает: каждая попытка входа на счету).
    protected virtual bool LoginMainClient => true;

    // Дополнительные настройки приложения этой фикстуры (ключ конфигурации -> значение).
    protected virtual IReadOnlyDictionary<string, string> ExtraSettings { get; } = new Dictionary<string, string>();

    public async Task InitializeAsync()
    {
        ConnectionString = await SharedPostgres.CreateDatabaseAsync();
        StoragePath = Path.Combine(Path.GetTempPath(), "hireflow-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(StoragePath);

        _factory = new ApiFactory(ConnectionString, StoragePath, LoginAttemptsPerMinute, ExtraSettings);
        Client = CreateClient(ApiFactory.BotApiKey);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        RecruiterId = (await CreateRecruiterAsync(RecruiterLogin, RecruiterPassword)).Id;
        if (LoginMainClient)
            await LoginAsync(Client, RecruiterLogin, RecruiterPassword);
    }

    // Создаёт рекрутера (или меняет ему пароль) через сервис, как это делает консольная команда.
    public async Task<RecruiterResponse> CreateRecruiterAsync(string login, string password)
    {
        await using var scope = Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRecruiterService>();
        return await service.SetPasswordAsync(login, password, CancellationToken.None);
    }

    // Создаёт клиент без ключа бота, уже вошедший под рекрутером (cookie хранит сам клиент).
    public async Task<HttpClient> CreateRecruiterClientAsync(string login = RecruiterLogin, string password = RecruiterPassword)
    {
        var client = CreateClient(apiKey: null);
        await LoginAsync(client, login, password);
        return client;
    }

    // Входит клиентом через /auth/login и проверяет, что вход удался.
    private static async Task LoginAsync(HttpClient client, string login, string password)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Login = login, Password = password });
        response.EnsureSuccessStatusCode();
    }

    // Создаёт клиент с заданным ключом бота в X-Api-Key; null — без заголовка.
    public HttpClient CreateClient(string? apiKey)
    {
        var client = _factory!.CreateClient();
        if (apiKey is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    public virtual async Task DisposeAsync()
    {
        try
        {
            Client?.Dispose();
            if (_factory is not null)
                await _factory.DisposeAsync();
        }
        finally
        {
            if (StoragePath is not null && Directory.Exists(StoragePath))
                Directory.Delete(StoragePath, recursive: true);
        }
    }
}
