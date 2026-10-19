using System.ComponentModel.DataAnnotations;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Application.JobApplications;

public record CreateApplicationRequest(
    long TelegramId,
    Guid VacancyId,
    int YearsOfExperience,
    string FileName,
    byte[] FileContent,
    string? Name = null,
    string? Phone = null);

public enum RecruiterDecision
{
    Invite,
    Reject,
}

public record DecisionRequest
{
    [Required]
    public RecruiterDecision? Decision { get; init; }

    [MaxLength(ApplicationStatusChange.ReasonMaxLength)]
    public string? Comment { get; init; }
}

public record ApplicationListItem(
    Guid Id,
    Guid VacancyId,
    string VacancyTitle,
    Guid CandidateId,
    string CandidateName,
    ApplicationStatus Status,
    int Score,
    ScreeningVerdict Verdict,
    DateTime CreatedAt);

public record StatusChangeResponse(
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    StatusChangedBy ChangedBy,
    string? Reason,
    DateTime ChangedAt,
    string? RecruiterLogin);

public record ApplicationResponse(
    Guid Id,
    Guid VacancyId,
    string VacancyTitle,
    Guid CandidateId,
    string CandidateName,
    string CandidatePhone,
    ApplicationStatus Status,
    int YearsOfExperience,
    string ResumeFileName,
    ScreeningResult Screening,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<StatusChangeResponse> History)
{
    // Собирает ответ API из отклика, вакансии, кандидата и логинов рекрутеров из истории.
    public static ApplicationResponse From(
        JobApplication a, Vacancy vacancy, Candidate candidate, IReadOnlyDictionary<Guid, string> recruiterLogins) => new(
        a.Id, a.VacancyId, vacancy.Title, a.CandidateId, candidate.Name, candidate.Phone,
        a.Status, a.YearsOfExperience, a.ResumeFileName, a.Screening,
        a.CreatedAt, a.UpdatedAt,
        a.History
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new StatusChangeResponse(
                h.FromStatus, h.ToStatus, h.ChangedBy, h.Reason, h.ChangedAt,
                h.RecruiterId is { } rid ? recruiterLogins.GetValueOrDefault(rid) : null))
            .ToList());
}

public record ResumeFile(Stream Content, string FileName, string ContentType);
