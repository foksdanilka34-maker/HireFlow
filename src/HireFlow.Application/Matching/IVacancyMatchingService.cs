namespace HireFlow.Application.Matching;

public interface IVacancyMatchingService
{
    // Подбирает открытые вакансии, которые резюме проходит, лучшие первыми. Резюме не сохраняется.
    Task<List<VacancyMatchResponse>> FindMatchesAsync(FindMatchesRequest request, CancellationToken ct);
}
