namespace HireFlow.Api.Hosting;

// Экспорт метрик, трейсов и логов по OTLP в коллектор; пустой адрес — телеметрия пишется, но никуда не отправляется.
public class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public string? OtlpEndpoint { get; set; }
    public TimeSpan ExportInterval { get; set; } = TimeSpan.FromSeconds(15);
}
