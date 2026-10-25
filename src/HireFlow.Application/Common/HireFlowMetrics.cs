using System.Diagnostics.Metrics;
using System.Text.Json;
using HireFlow.Application.JobApplications;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Screening;

namespace HireFlow.Application.Common;

// Доменные метрики API (meter HireFlow); в тегах только перечисления, без персональных данных и id.
public class HireFlowMetrics
{
    public const string MeterName = "HireFlow";

    // Границы корзин скрининга в секундах: 60 — порог SLO (99% откликов быстрее минуты).
    private static readonly double[] ScreeningDurationBuckets = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120];

    // Границы корзин запросов публичного API в секундах: 1 — порог SLO (99% небольших скринингов быстрее 1 с).
    private static readonly double[] PublicApiDurationBuckets = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10];

    private readonly Counter<long> _applicationsCreated;
    private readonly Histogram<double> _screeningDuration;
    private readonly Counter<long> _decisions;
    private readonly Counter<long> _uploadsLimited;
    private readonly Counter<long> _logins;
    private readonly Histogram<double> _publicApiDuration;

    // Создаёт meter через фабрику DI и регистрирует инструменты.
    public HireFlowMetrics(IMeterFactory meterFactory)
    {
        Meter = meterFactory.Create(MeterName);
        _applicationsCreated = Meter.CreateCounter<long>("hireflow.applications.created", description: "Созданные отклики");
        _screeningDuration = Meter.CreateHistogram("hireflow.screening.duration", "s", "Чтение резюме и скоринг",
            tags: null, advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = ScreeningDurationBuckets });
        _decisions = Meter.CreateCounter<long>("hireflow.applications.decisions", description: "Решения рекрутера");
        _uploadsLimited = Meter.CreateCounter<long>("hireflow.resume_uploads.limited", description: "Отказы по лимиту загрузок");
        _logins = Meter.CreateCounter<long>("hireflow.recruiter.logins", description: "Попытки входа рекрутера");
        _publicApiDuration = Meter.CreateHistogram("hireflow.public_api.request.duration", "s", "Запросы публичного API",
            tags: null, advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = PublicApiDurationBuckets });
    }

    public Meter Meter { get; }

    // Учитывает созданный отклик с вердиктом скрининга.
    public void ApplicationCreated(ScreeningVerdict verdict) =>
        _applicationsCreated.Add(1, new KeyValuePair<string, object?>("verdict", ToTag(verdict)));

    // Записывает длительность чтения резюме и скоринга в секундах.
    public void ScreeningDuration(TimeSpan duration) => _screeningDuration.Record(duration.TotalSeconds);

    // Учитывает решение рекрутера и статус, из которого оно принято.
    public void Decision(RecruiterDecision decision, ApplicationStatus fromStatus) =>
        _decisions.Add(1,
            new KeyValuePair<string, object?>("decision", ToTag(decision)),
            new KeyValuePair<string, object?>("from_status", ToTag(fromStatus)));

    // Учитывает отказ в загрузке резюме по дневному лимиту.
    public void ResumeUploadLimited() => _uploadsLimited.Add(1);

    // Учитывает попытку входа рекрутера: success, failure или limited.
    public void RecruiterLogin(string result) =>
        _logins.Add(1, new KeyValuePair<string, object?>("result", result));

    // Записывает длительность запроса публичного API: клиент, шаблон маршрута, код ответа и размерный класс тела.
    public void PublicApiRequest(string client, string route, int statusCode, string size, TimeSpan duration) =>
        _publicApiDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("client", client),
            new KeyValuePair<string, object?>("http_route", route),
            new KeyValuePair<string, object?>("status_code", statusCode),
            new KeyValuePair<string, object?>("size", size));

    // Переводит значение перечисления в snake_case, как в API и БД.
    public static string ToTag<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
}
