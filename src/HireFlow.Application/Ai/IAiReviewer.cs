using HireFlow.Domain.Ai;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Application.Ai;

public interface IAiReviewer
{
    // Проверяет признаки и короткие фрагменты резюме с удалёнными распознаваемыми личными данными.
    Task<AiReviewResult> ReviewAsync(JobApplication application, VacancyRequirements requirements, string? candidateName, string? phone, CancellationToken ct);
    Task<AiReviewResult> ReviewAnswersAsync(JobApplication application, string? candidateName, string? phone, CancellationToken ct);
}
