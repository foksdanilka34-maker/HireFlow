namespace HireFlow.Application.Candidates;

public interface ICandidateService
{
    // Возвращает кандидата по Telegram ID.
    Task<CandidateResponse> GetAsync(long telegramId, CancellationToken ct);

    // Создаёт кандидата, а если он уже есть — обновляет имя и телефон.
    Task<CandidateResponse> UpsertAsync(long telegramId, CandidateRequest request, CancellationToken ct);

    // Возвращает отклики кандидата со статусами, как их видит кандидат.
    Task<List<CandidateApplicationResponse>> GetApplicationsAsync(long telegramId, CancellationToken ct);
}
