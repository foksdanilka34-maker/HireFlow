using System.ComponentModel.DataAnnotations;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Application.Ai;

public record VacancyAiRequest
{
    [Required, MaxLength(200)] public string Title { get; init; } = "";
    [MaxLength(200)] public string? Direction { get; init; }
    [MaxLength(5000)] public string? Description { get; init; }
    public List<string> RequiredSkills { get; init; } = [];
    public List<string> NiceToHaveSkills { get; init; } = [];
    [Range(0, 50)] public int MinYearsOfExperience { get; init; }
}

public record VacancyAiResult(string Description, List<string> Suggestions, List<VacancyQuestion> Questions);

public interface IVacancyAiAssistant
{
    Task<VacancyAiResult> SuggestAsync(VacancyAiRequest request, CancellationToken ct);
}
