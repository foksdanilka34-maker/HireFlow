using System.Net;
using HireFlow.Api.Tests.Infrastructure;

namespace HireFlow.Api.Tests;

// Health-эндпоинты на стенде с БД: без ключа бота и cookie (так их опрашивает Docker).
public class HealthApiTests(ApiAppFixture app) : IClassFixture<ApiAppFixture>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_Anonymous_ReturnsOk(string path)
    {
        using var client = app.CreateClient(apiKey: null);

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

// Приложение без доступной БД: строка подключения на закрытый порт, миграции не применяются.
public class UnavailableDatabaseFixture : IAsyncLifetime
{
    private const string Password = "secret-db-password";

    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), "hireflow-tests", Guid.NewGuid().ToString("N"));
    private ApiFactory? _factory;

    public HttpClient Client { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_storagePath);
        _factory = new ApiFactory($"Host=127.0.0.1;Port=1;Database=hireflow;Username=hireflow;Password={Password};Timeout=2", _storagePath);
        Client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null)
            await _factory.DisposeAsync();
        if (Directory.Exists(_storagePath))
            Directory.Delete(_storagePath, recursive: true);
    }
}

public class HealthUnavailableDatabaseTests(UnavailableDatabaseFixture app) : IClassFixture<UnavailableDatabaseFixture>
{
    [Fact]
    public async Task Ready_DatabaseUnavailable_Returns503WithoutDetails()
    {
        using var response = await app.Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        // Только статус: ни текста исключения, ни хоста и пароля из строки подключения.
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Live_DatabaseUnavailable_ReturnsOk()
    {
        using var response = await app.Client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
