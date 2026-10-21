using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Api.Database;

public static class MigrateCommand
{
    // Применяет миграции EF Core к базе из консоли; возвращает код выхода.
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInformation("Migrations applied");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError("Migrations failed: {ErrorType}", ex.GetType().Name);
            return 1;
        }
    }
}
