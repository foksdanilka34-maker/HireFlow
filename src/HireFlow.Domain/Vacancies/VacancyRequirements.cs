using System.ComponentModel.DataAnnotations;

namespace HireFlow.Domain.Vacancies;

public record VacancyRequirements : IValidatableObject
{
    public const int DefaultBorderlineWidth = 10;

    [Required, MinLength(1)]
    public List<Skill> RequiredSkills { get; init; } = [];

    [Required]
    public List<Skill> NiceToHaveSkills { get; init; } = [];

    [Range(0, 50)]
    public int MinYearsOfExperience { get; init; }

    [Range(0, 100)]
    public int PassingScore { get; init; }

    [Range(0, 100)]
    public int BorderlineWidth { get; init; } = DefaultBorderlineWidth;

    // Проверяет правила, в которых участвуют несколько полей.
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (BorderlineWidth > PassingScore)
            yield return new("BorderlineWidth must not exceed PassingScore.", [nameof(BorderlineWidth)]);
    }
}

public record Skill
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = "";

    [Required]
    public List<string> Synonyms { get; init; } = [];
}
