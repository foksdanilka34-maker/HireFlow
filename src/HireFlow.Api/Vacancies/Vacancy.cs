namespace HireFlow.Api.Vacancies;

public record VacancyRequirements (
    List<string> RequiredSkills,
    List<string> NiceToHaveSkills,
    int MinYearsOfExperience,
    int PassingScore
);

public record Vacancy(
    int Id,
    string Title,
    string? Description,
    VacancyRequirements Requirements,
    bool IsOpen,
    DateTime CreatedAt
);