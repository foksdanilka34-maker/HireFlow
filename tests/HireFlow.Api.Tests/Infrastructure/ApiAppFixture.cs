using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests.Infrastructure;

// Фикстура тестового класса: своя база с применёнными миграциями, своё приложение и своя папка хранилища.
public class ApiAppFixture : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public string ConnectionString { get; private set; } = null!;
    public string StoragePath { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _factory.Services;

    public async Task InitializeAsync()
    {
        ConnectionString = await SharedPostgres.CreateDatabaseAsync();
        StoragePath = Path.Combine(Path.GetTempPath(), "hireflow-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(StoragePath);

        _factory = new ApiFactory(ConnectionString, StoragePath);
        Client = _factory.CreateClient();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        if (Directory.Exists(StoragePath))
            Directory.Delete(StoragePath, recursive: true);
    }
}
