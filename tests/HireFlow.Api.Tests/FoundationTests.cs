using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Проверяет сам стенд: настройки доходят до Program, миграции применены к настоящему PostgreSQL.
public class FoundationTests(ApiAppFixture app) : ApiTestBase(app)
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await Client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Settings_ReachProgramBeforeBuild()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(App.ConnectionString, db.Database.GetConnectionString());
        var storage = scope.ServiceProvider.GetRequiredService<HireFlow.Application.Resumes.IFileStorage>();
        var key = await storage.SaveAsync([1, 2, 3], "bin", CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(App.StoragePath, key)));
    }

    [Fact]
    public async Task Migrations_AreAppliedToRealPostgres()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("AddConcurrencyTokens"));
    }

    [Fact]
    public async Task Database_UsesPostgresWithUuidV7()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vacancy = await CreateVacancyAsync([Unique("skill")]);

        Assert.Equal(7, vacancy.Id.Version);
        Assert.True(await db.Database.CanConnectAsync());
    }
}
