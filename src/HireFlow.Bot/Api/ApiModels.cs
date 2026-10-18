using System.Text.Json.Serialization;

namespace HireFlow.Bot.Api;

public record VacancyDto(Guid Id, string Title, string Direction, string? Description = null, string? Status = null);

public record MatchesDto(int? YearsOfExperience, bool ExperienceRequired, bool PhoneFound, List<VacancyDto> Vacancies);

public record CandidateDto(Guid Id, long TelegramId, string Name);

public enum CandidateStatus
{
    InReview,
    Invited,
    Rejected,
}

public record CandidateApplicationDto(Guid Id, Guid VacancyId, string VacancyTitle, CandidateStatus Status);

public record ApplicationCreatedDto(Guid Id);

public enum NotificationKind
{
    Invited,
    Rejected,
}

public record DueNotificationDto(Guid Id, long TelegramId, NotificationKind Type, string VacancyTitle);

public record CreateApplicationCall(
    long TelegramId,
    Guid VacancyId,
    int YearsOfExperience,
    byte[] File,
    string FileName,
    string Name,
    string? Phone);

// Черновик диалога, который хранится в BotSession.draft. Телефон и текст резюме сюда не пишутся.
public class BotDraft
{
    [JsonPropertyName("resume_file_id")]
    public string? ResumeFileId { get; set; }

    [JsonPropertyName("resume_file_name")]
    public string? ResumeFileName { get; set; }

    [JsonPropertyName("years_of_experience")]
    public int? YearsOfExperience { get; set; }

    [JsonPropertyName("phone_found")]
    public bool PhoneFound { get; set; }

    [JsonPropertyName("vacancy_ids")]
    public List<Guid> VacancyIds { get; set; } = [];

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pending_vacancy_id")]
    public Guid? PendingVacancyId { get; set; }
}

public class BotSessionState
{
    public string Step { get; set; } = "";
    public Guid? LinkedVacancyId { get; set; }
    public BotDraft Draft { get; set; } = new();
}
