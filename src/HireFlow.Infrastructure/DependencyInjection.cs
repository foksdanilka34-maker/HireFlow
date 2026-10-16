using HireFlow.Application.Common;
using HireFlow.Application.Resumes;
using HireFlow.Infrastructure.Files;
using HireFlow.Infrastructure.Persistence;
using HireFlow.Infrastructure.Resumes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Infrastructure;

public static class DependencyInjection
{
    // Регистрирует в DI всё из слоя Infrastructure: базу, хранилище файлов, извлечение текста.
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString, string storagePath)
    {
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IFileStorage>(new LocalFileStorage(storagePath));
        services.AddSingleton<IResumeTextExtractor, ResumeTextExtractor>();

        return services;
    }
}
