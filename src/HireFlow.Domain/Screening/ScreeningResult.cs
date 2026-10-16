namespace HireFlow.Domain.Screening;

public record ScreeningResult(
    int Score,
    ScreeningVerdict Verdict,
    List<string> MissingRequiredSkills,
    List<string> MissingNiceToHaveSkills,
    bool ExperienceIsEnough);
