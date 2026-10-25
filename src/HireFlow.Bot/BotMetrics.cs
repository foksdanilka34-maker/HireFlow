using System.Diagnostics.Metrics;

namespace HireFlow.Bot;

// Метрики бота (meter HireFlow.Bot); в тегах только результаты и имена операций, без Telegram ID и других ПД.
public class BotMetrics
{
    public const string MeterName = "HireFlow.Bot";

    private readonly Counter<long> _messages;
    private readonly Counter<long> _messagesDropped;
    private readonly Counter<long> _pollingErrors;
    private readonly Counter<long> _apiErrors;
    private readonly Counter<long> _notifications;

    // Создаёт meter через фабрику DI и регистрирует инструменты.
    public BotMetrics(IMeterFactory meterFactory)
    {
        Meter = meterFactory.Create(MeterName);
        _messages = Meter.CreateCounter<long>("hireflow.bot.messages", description: "Обработанные сообщения");
        _messagesDropped = Meter.CreateCounter<long>("hireflow.bot.messages.dropped", description: "Сообщения, отброшенные из-за полной очереди");
        _pollingErrors = Meter.CreateCounter<long>("hireflow.bot.polling.errors", description: "Сбои GetUpdates");
        _apiErrors = Meter.CreateCounter<long>("hireflow.bot.api.errors", description: "Сбои вызовов API");
        _notifications = Meter.CreateCounter<long>("hireflow.bot.notifications", description: "Отправка уведомлений");

        // Ряды с 0 сразу после старта: иначе первая точка приходит уже с k ≥ 1 и increase не видит первую пачку.
        _messagesDropped.Add(0);
        _pollingErrors.Add(0);
    }

    public Meter Meter { get; }

    // Учитывает обработанное сообщение: ok или failed.
    public void MessageHandled(bool ok) =>
        _messages.Add(1, new KeyValuePair<string, object?>("result", ok ? "ok" : "failed"));

    // Учитывает сообщение, отброшенное из-за переполнения очереди.
    public void MessageDropped() => _messagesDropped.Add(1);

    // Учитывает сбой опроса Telegram.
    public void PollingError() => _pollingErrors.Add(1);

    // Учитывает сбой вызова API с именем операции.
    public void ApiError(string operation) =>
        _apiErrors.Add(1, new KeyValuePair<string, object?>("operation", operation));

    // Учитывает результат отправки уведомления: sent или failed.
    public void NotificationSent(bool ok) =>
        _notifications.Add(1, new KeyValuePair<string, object?>("result", ok ? "sent" : "failed"));
}
