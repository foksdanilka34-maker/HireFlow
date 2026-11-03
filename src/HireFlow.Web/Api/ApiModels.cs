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

public record VacancyQuestionDto(string Text, string? Criterion);

public record VacancyAiRequestDto(string Title, string? Direction, string? Description, List<string> RequiredSkills, List<string> NiceToHaveSkills, int MinYearsOfExperience);
public record VacancyAiResultDto(string Description, List<string> Suggestions, List<VacancyQuestionDto> Questions);

public record VacancyDto(
    Guid Id,
    string Title,
    string Direction,
    string? Description,
    VacancyStatus Status,
    RequirementsDto Requirements,
    int FreezePeriodDays,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<VacancyQuestionDto>? Questions = null);

public record VacancyRequestDto(
    string Title,
    string Direction,
    string? Description,
    RequirementsDto Requirements,
    int FreezePeriodDays,
    List<VacancyQuestionDto>? Questions = null);

public enum ApplicationStatus
{
    Received,
    AiReview,
    AwaitingQuestions,
    AiAnswersReview,
    Expired,
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

public record AiReviewDto(string Recommendation, int Score, string Explanation, string Model, string PromptVersion, DateTime ReviewedAt);
public record ApplicationQuestionDto(int Position, string Text, string? Criterion, string? Answer);

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
    AiReviewDto? AiReview,
    bool AiReviewUnavailable,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<StatusChangeDto> History,
    List<ApplicationQuestionDto>? Questions = null,
    AiReviewDto? AnswersReview = null,
    bool AnswersReviewUnavailable = false,
    DateTime? QuestionsDeadline = null);

public record DecisionRequestDto(RecruiterDecision Decision, string? Comment);

public record ResumeTextDto(string Text);
