using System.Diagnostics;
using HireFlow.Bot.Dialog;

namespace HireFlow.Bot;

// Трассировка бота: свой источник спанов и фильтр исходящих запросов.
public static class BotTelemetry
{
    public const string SourceName = "HireFlow.Bot";
    public const string MessageKindTag = "bot.message.kind";

    public static readonly ActivitySource Source = new(SourceName);

    // Запросы к Telegram не трассируются: токен бота находится в пути запроса.
    public static bool ShouldTrace(HttpRequestMessage request) =>
        request.RequestUri is not { IsAbsoluteUri: true } uri
        || !string.Equals(uri.Host, "api.telegram.org", StringComparison.OrdinalIgnoreCase);

    // Вид сообщения для тега спана: без текста и Telegram ID.
    public static string MessageKind(IncomingMessage message) =>
        message.Document is not null ? "document"
        : message.Text?.StartsWith('/') == true ? "command"
        : "text";
}
