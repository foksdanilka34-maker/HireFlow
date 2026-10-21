namespace HireFlow.Web.Api;

public record RecruiterDto(Guid Id, string Login);

public record LoginRequestDto(string Login, string Password);

public enum VacancyStatus
{
    Open,
    Closed,
    Archived,
}

public record SkillDto(string Name, List<string> Synonyms);

public record RequirementsDto(
    List<SkillDto> RequiredSkills,
    List<SkillDto> NiceToHaveSkills,
    int MinYearsOfExperience,
    int PassingScore,
    int BorderlineWidth);

public record VacancyDto(
    Guid Id,
    string Title,
    string Direction,
    string? Description,
    VacancyStatus Status,
    RequirementsDto Requirements,
    int FreezePeriodDays,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record VacancyRequestDto(
    string Title,
    string Direction,
    string? Description,
    RequirementsDto Requirements,
    int FreezePeriodDays);
