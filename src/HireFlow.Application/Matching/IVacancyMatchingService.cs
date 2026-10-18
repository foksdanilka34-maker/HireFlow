namespace HireFlow.Application.Matching;

public interface IVacancyMatchingService
{
    // Подбирает открытые вакансии, которые резюме проходит, лучшие первыми. Опыт не передан — ищет его в резюме,
    // не нашёл — подбор не выполняется. Резюме не сохраняется.
    Task<FindMatchesResponse> FindMatchesAsync(FindMatchesRequest request, CancellationToken ct);
}
