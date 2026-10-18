using Npgsql;
using Testcontainers.PostgreSql;

namespace HireFlow.Api.Tests.Infrastructure;

// Один контейнер PostgreSQL на весь прогон; каждый тестовый класс получает в нём собственную базу.
public static class SharedPostgres
{
    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(StartAsync);

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        // postgres:18 нужен ради uuidv7().
        var container = new PostgreSqlBuilder("postgres:18").Build();
        await container.StartAsync();

        // Контейнер убирает и Ryuk, но при штатном выходе останавливаем его сами.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => container.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return container;
    }

    // Создаёт новую пустую базу в контейнере и возвращает строку подключения к ней.
    public static async Task<string> CreateDatabaseAsync()
    {
        var container = await Container.Value;
        var databaseName = $"test_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = databaseName };
        return builder.ConnectionString;
    }
}
