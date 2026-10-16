using System.ComponentModel.DataAnnotations;
using HireFlow.Domain.Candidates;

namespace HireFlow.Application.Candidates;

public record CandidateRequest
{
    [Required, MaxLength(Candidate.NameMaxLength)]
    public string Name { get; init; } = "";

    [Required, RegularExpression(@"^\+?\d{7,15}$")]
    public string Phone { get; init; } = "";
}

public record CandidateResponse(
    Guid Id,
    long TelegramId,
    string Name,
    string Phone,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    // Собирает ответ API из сущности.
    public static CandidateResponse From(Candidate c) => new(
        c.Id, c.TelegramId, c.Name, c.Phone, c.CreatedAt, c.UpdatedAt);
}

public enum CandidateStatus
{
    InReview,
    Invited,
    Rejected,
}

public record CandidateApplicationResponse(Guid Id, string VacancyTitle, CandidateStatus Status, DateTime CreatedAt);
