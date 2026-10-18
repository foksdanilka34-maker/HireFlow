namespace HireFlow.Application.BotSessions;

public interface IBotSessionService
{
    // Возвращает сессию диалога бота по Telegram ID.
    Task<BotSessionResponse> GetAsync(long telegramId, CancellationToken ct);

    // Создаёт сессию или заменяет её состояние. Шаг и черновик API не интерпретирует.
    Task<BotSessionResponse> UpsertAsync(long telegramId, BotSessionRequest request, CancellationToken ct);
}
