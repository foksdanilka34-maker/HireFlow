namespace HireFlow.Bot;

// Экспорт метрик по OTLP; пустой адрес — метрики пишутся, но никуда не отправляются.
public class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public string? OtlpMetricsEndpoint { get; set; }
    public TimeSpan ExportInterval { get; set; } = TimeSpan.FromSeconds(15);
}
