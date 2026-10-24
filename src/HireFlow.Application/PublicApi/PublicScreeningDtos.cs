using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Application.PublicApi;

// Запрос скрининга: ровно одно из File (PDF/DOCX) и Text; резюме и требования нигде не сохраняются.
public record PublicScreeningRequest(
    byte[]? File,
    string? Text,
    VacancyRequirements? Requirements,
    int? YearsOfExperience);

public enum YearsOfExperienceSource
{
    Request,
    Resume,
    None,
}

public record PublicScreeningResponse(
    int Score,
    ScreeningVerdict Verdict,
    List<string> MatchedRequiredSkills,
    List<string> MissingRequiredSkills,
    List<string> MatchedNiceToHaveSkills,
    List<string> MissingNiceToHaveSkills,
    int? YearsOfExperience,
    YearsOfExperienceSource YearsOfExperienceSource,
    bool ExperienceIsEnough);

public record PublicUsageResponse(int RequestsToday, int DailyQuota, DateTime ResetsAt);
