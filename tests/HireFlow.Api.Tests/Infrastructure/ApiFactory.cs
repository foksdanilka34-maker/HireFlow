using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HireFlow.Api.Tests.Infrastructure;

// Фабрика приложения: строка подключения и папка хранилища задаются так, чтобы Program увидел их до Build().
public class ApiFactory(string connectionString, string storagePath) : WebApplicationFactory<Program>
{
    // Служебный ключ бота для тестов: без него API не стартует.
    public const string BotApiKey = "test-bot-api-key";

    // Задержка отказа задаётся явно, чтобы тесты не зависели от appsettings.json.
    public static readonly TimeSpan RejectionDelay = TimeSpan.FromHours(4);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // «Testing», а не «Development»: так не подтянутся user secrets и appsettings.Development.json разработчика.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", connectionString);
        builder.UseSetting("Storage:LocalPath", storagePath);
        builder.UseSetting("BotApi:ApiKey", BotApiKey);
        builder.UseSetting("ResumeUploads:DailyLimit", "3");
        builder.UseSetting("Notifications:RejectionDelay", RejectionDelay.ToString());
    }
}
