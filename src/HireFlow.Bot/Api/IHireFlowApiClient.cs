namespace HireFlow.Bot.Api;

public interface IHireFlowApiClient
{
    // Возвращает вакансию по id.
    Task<ApiResult<VacancyDto>> GetVacancyAsync(Guid id, CancellationToken ct);

    // Возвращает все открытые вакансии.
    Task<ApiResult<List<VacancyDto>>> GetOpenVacanciesAsync(CancellationToken ct);

    // Подбирает вакансии под резюме; без опыта API пробует найти его в резюме.
    Task<ApiResult<MatchesDto>> FindMatchesAsync(long telegramId, byte[] file, string fileName, int? yearsOfExperience, CancellationToken ct);

    // Создаёт отклик (и кандидата при первом отклике).
    Task<ApiResult<ApplicationCreatedDto>> CreateApplicationAsync(CreateApplicationCall call, CancellationToken ct);

    // Возвращает кандидата по Telegram ID.
    Task<ApiResult<CandidateDto>> GetCandidateAsync(long telegramId, CancellationToken ct);

    // Возвращает отклики кандидата со статусами, как их видит кандидат.
    Task<ApiResult<List<CandidateApplicationDto>>> GetCandidateApplicationsAsync(long telegramId, CancellationToken ct);

    // Возвращает сессию диалога.
    Task<ApiResult<BotSessionState>> GetSessionAsync(long telegramId, CancellationToken ct);

    // Сохраняет сессию диалога.
    Task<ApiResult<BotSessionState>> PutSessionAsync(long telegramId, BotSessionState state, CancellationToken ct);

    // Возвращает уведомления, которые пора отправить.
    Task<ApiResult<List<DueNotificationDto>>> GetDueNotificationsAsync(CancellationToken ct);

    // Отмечает уведомление отправленным.
    Task<ApiStatus> MarkNotificationSentAsync(Guid id, CancellationToken ct);

    // Отмечает, что отправить уведомление не удалось.
    Task<ApiStatus> MarkNotificationFailedAsync(Guid id, string error, CancellationToken ct);
}
