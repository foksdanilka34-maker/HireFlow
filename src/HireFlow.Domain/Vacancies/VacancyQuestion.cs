using System.ComponentModel.DataAnnotations;

namespace HireFlow.Domain.Vacancies;

public record VacancyQuestion
{
    [Required, MaxLength(500)]
    public string Text { get; init; } = "";
    [MaxLength(1000)]
    public string? Criterion { get; init; }
}
