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

public enum ApplicationStatus
{
    Received,
    RecruiterReview,
    AutoRejected,
    Invited,
    Rejected,
}

public enum ScreeningVerdict
{
    Passed,
    Borderline,
    Failed,
}

public enum ResumeFormat
{
    Pdf,
    Docx,
}

public enum StatusChangedBy
{
    System,
    Ai,
    Recruiter,
}

public enum RecruiterDecision
{
    Invite,
    Reject,
}

public record ApplicationListItemDto(
    Guid Id,
    Guid VacancyId,
    string VacancyTitle,
    Guid CandidateId,
    string CandidateName,
    ApplicationStatus Status,
    int Score,
    ScreeningVerdict Verdict,
    DateTime CreatedAt);

public record ScreeningDto(
    int Score,
    ScreeningVerdict Verdict,
    List<string> MissingRequiredSkills,
    List<string> MissingNiceToHaveSkills,
    bool ExperienceIsEnough);

public record StatusChangeDto(
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    StatusChangedBy ChangedBy,
    string? Reason,
    DateTime ChangedAt,
    string? RecruiterLogin);

public record ApplicationDto(
    Guid Id,
    Guid VacancyId,
    string VacancyTitle,
    Guid CandidateId,
    string CandidateName,
    string CandidatePhone,
    ApplicationStatus Status,
    int YearsOfExperience,
    string ResumeFileName,
    ResumeFormat ResumeFormat,
    bool RejectionSent,
    ScreeningDto Screening,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<StatusChangeDto> History);

public record DecisionRequestDto(RecruiterDecision Decision, string? Comment);

public record ResumeTextDto(string Text);
