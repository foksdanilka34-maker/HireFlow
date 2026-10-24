namespace HireFlow.Application.PublicApi;

public class PublicApiOptions
{
    public const string SectionName = "PublicApi";

    public int RequestsPerMinute { get; init; } = 60;
    // Дневная квота нового клиента; квоты существующих меняются в БД.
    public int DailyQuota { get; init; } = 1000;
    public TimeSpan ExtractionTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
