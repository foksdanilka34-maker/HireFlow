using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HireFlow.Api.Tests.Infrastructure;

// Фабрика приложения: строка подключения и папка хранилища задаются так, чтобы Program увидел их до Build().
public class ApiFactory(string connectionString, string storagePath) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // «Testing», а не «Development»: так не подтянутся user secrets и appsettings.Development.json разработчика.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", connectionString);
        builder.UseSetting("Storage:LocalPath", storagePath);
    }
}
