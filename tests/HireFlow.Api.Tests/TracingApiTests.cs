using System.Collections;
using System.Diagnostics;
using HireFlow.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace HireFlow.Api.Tests;

// Потокобезопасный приёмник завершённых спанов для InMemory-экспортёра.
public sealed class SpanSink : ICollection<Activity>
{
    private readonly List<Activity> _items = [];

    public int Count { get { lock (_items) return _items.Count; } }
    public bool IsReadOnly => false;

    public void Add(Activity item) { lock (_items) _items.Add(item); }
    public bool Remove(Activity item) { lock (_items) return _items.Remove(item); }
    public void Clear() { lock (_items) _items.Clear(); }
    public bool Contains(Activity item) { lock (_items) return _items.Contains(item); }
    public void CopyTo(Activity[] array, int arrayIndex) { lock (_items) _items.CopyTo(array, arrayIndex); }
    public IEnumerator<Activity> GetEnumerator() => ((IEnumerable<Activity>)Snapshot()).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Копия накопленных спанов: безопасно читать, пока экспортёр дописывает новые.
    public Activity[] Snapshot() { lock (_items) return [.. _items]; }
}

// Приложение с InMemory-экспортёром трейсов; экспортёр стоит после UrlTagsRemover и получает очищенные спаны.
public class TracingFixture : ApiAppFixture
{
    public SpanSink Spans { get; } = new();

    protected override void ConfigureServices(IServiceCollection services) =>
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(Spans));
}

// Трассировка API: Telegram ID не попадает в спаны, healthcheck не создаёт спанов.
public class TracingApiTests(TracingFixture app) : ApiTestBase(app), IClassFixture<TracingFixture>
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    // Шаблон маршрута кандидата: в нём нет самого ID.
    private const string CandidateRoute = "/candidates/{telegramId:long:min(1)}";

    [Fact]
    public async Task GetCandidate_ExportedSpan_HasRouteTemplateAndNoTelegramId()
    {
        var candidate = await CreateCandidateAsync();
        var id = candidate.TelegramId.ToString();
        var traceId = NewTraceId();

        using var response = await GetAsync($"/candidates/{id}", traceId);
        response.EnsureSuccessStatusCode();
        var server = await WaitForServerSpanAsync(traceId);

        Assert.Equal(CandidateRoute, server.GetTagItem("http.route"));
        foreach (var tag in new[] { "url.path", "url.full", "url.query" })
            Assert.Null(server.GetTagItem(tag));
        // Проверяем все спаны трассы (включая запросы к БД) и имена спанов, а не только серверный спан.
        var spans = SpansOf(traceId);
        Assert.NotEmpty(spans);
        foreach (var span in spans)
        {
            Assert.DoesNotContain(id, span.DisplayName);
            foreach (var tag in span.TagObjects)
                Assert.DoesNotContain(id, tag.Value?.ToString() ?? "");
        }
    }

    [Fact]
    public async Task HealthReady_DoesNotCreateSpan()
    {
        var traceId = NewTraceId();

        using var health = await GetAsync("/health/ready", traceId);
        health.EnsureSuccessStatusCode();
        // Контрольный запрос в той же трассе: когда его спан экспортирован, спан healthcheck уже был бы виден.
        using var control = await GetAsync($"/candidates/{NewTelegramId()}", traceId);
        await WaitForServerSpanAsync(traceId);

        var server = Assert.Single(SpansOf(traceId), s => s.Kind == ActivityKind.Server);
        Assert.DoesNotContain("/health", (string?)server.GetTagItem("http.route"));
    }

    // Отправляет GET с заданным trace id в traceparent: спаны других тестов и приложений процесса не мешают.
    private async Task<HttpResponseMessage> GetAsync(string path, string traceId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");
        return await Client.SendAsync(request);
    }

    // Возвращает новый случайный trace id в виде строки.
    private static string NewTraceId() => ActivityTraceId.CreateRandom().ToString();

    // Возвращает экспортированные спаны указанной трассы.
    private List<Activity> SpansOf(string traceId) =>
        app.Spans.Snapshot().Where(s => s.TraceId.ToString() == traceId).ToList();

    // Ждёт экспорта серверного спана трассы: он завершается уже после отправки ответа.
    private async Task<Activity> WaitForServerSpanAsync(string traceId)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (DateTime.UtcNow < deadline)
        {
            var server = SpansOf(traceId).FirstOrDefault(s => s.Kind == ActivityKind.Server);
            if (server is not null)
                return server;
            await Task.Delay(20);
        }
        throw new TimeoutException("Серверный спан не экспортирован");
    }
}
