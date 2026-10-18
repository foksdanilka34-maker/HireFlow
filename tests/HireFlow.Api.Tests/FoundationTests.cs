using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Проверяет сам стенд: настройки доходят до Program до Build().
public class FoundationTests(ApiAppFixture app) : ApiTestBase(app)
{
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
}
