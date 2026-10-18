using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests.Infrastructure;

// Фикстура тестового класса: своя база с применёнными миграциями, своё приложение и своя папка хранилища.
public class ApiAppFixture : IAsyncLifetime
{
    private ApiFactory? _factory;

    public string ConnectionString { get; private set; } = null!;
    public string StoragePath { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _factory!.Services;

    public async Task InitializeAsync()
    {
        ConnectionString = await SharedPostgres.CreateDatabaseAsync();
        StoragePath = Path.Combine(Path.GetTempPath(), "hireflow-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(StoragePath);

        _factory = new ApiFactory(ConnectionString, StoragePath);
        Client = CreateClient(ApiFactory.BotApiKey);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    // Создаёт клиент с заданным ключом бота в X-Api-Key; null — без заголовка.
    public HttpClient CreateClient(string? apiKey)
    {
        var client = _factory!.CreateClient();
        if (apiKey is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    public async Task DisposeAsync()
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
