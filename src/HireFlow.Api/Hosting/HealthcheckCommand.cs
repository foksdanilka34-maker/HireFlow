namespace HireFlow.Api.Hosting;

public static class HealthcheckCommand
{
    // Запрашивает /health/ready локального API для healthcheck контейнера; 0 — готов, 1 — нет.
    public static async Task<int> RunAsync()
    {
        var ports = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");
        var port = ports?.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "8080";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await http.GetAsync($"http://localhost:{port}/health/ready");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception)
        {
            return 1;
        }
    }
}
