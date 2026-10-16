using System.ComponentModel.DataAnnotations;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Application.Vacancies;

public record VacancyRequest
{
    [Required, MaxLength(Vacancy.TitleMaxLength)]
    public string Title { get; init; } = "";

    [Required, MaxLength(Vacancy.DirectionMaxLength)]
    public string Direction { get; init; } = "";

    [MaxLength(5000)]
    public string? Description { get; init; }

    [Required]
    public VacancyRequirements Requirements { get; init; } = null!;

    [Range(0, 3650)]
    public int FreezePeriodDays { get; init; } = Vacancy.DefaultFreezePeriodDays;
}

public record VacancyResponse(
    Guid Id,
    string Title,
    string Direction,
    string? Description,
    VacancyStatus Status,
    VacancyRequirements Requirements,
    int FreezePeriodDays,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    // Собирает ответ API из сущности.
    public static VacancyResponse From(Vacancy v) => new(
        v.Id, v.Title, v.Direction, v.Description, v.Status, v.Requirements, v.FreezePeriodDays, v.CreatedAt, v.UpdatedAt);
}
