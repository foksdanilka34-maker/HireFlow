using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HireFlow.Api.Tests.Infrastructure;

// Фабрика приложения: строка подключения и папка хранилища задаются так, чтобы Program увидел их до Build().
public class ApiFactory(string connectionString, string storagePath, int loginAttemptsPerMinute = ApiFactory.DefaultLoginAttempts,
    IReadOnlyDictionary<string, string>? extraSettings = null)
    : WebApplicationFactory<Program>
{
    // Служебный ключ бота для тестов: без него API не стартует.
    public const string BotApiKey = "test-bot-api-key";

    // Лимит попыток входа по умолчанию: большой, чтобы входы фикстур и тестов не упирались в 429.
    public const int DefaultLoginAttempts = 1000;

    // Адрес кабинета рекрутера, которому разрешён CORS: в окружении Testing appsettings.Development.json не читается.
    public const string WebOrigin = "http://localhost:5200";

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
        builder.UseSetting("Web:Origins:0", WebOrigin);
        builder.UseSetting("RecruiterAuth:LoginAttemptsPerMinute", loginAttemptsPerMinute.ToString());

        // Дополнительные настройки конкретной фикстуры (например, Hosting:*).
        foreach (var (key, value) in extraSettings ?? new Dictionary<string, string>())
            builder.UseSetting(key, value);
    }
}
