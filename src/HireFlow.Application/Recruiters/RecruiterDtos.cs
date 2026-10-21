using System.ComponentModel.DataAnnotations;
using HireFlow.Domain.Recruiters;

namespace HireFlow.Application.Recruiters;

public record LoginRequest
{
    public const int PasswordMaxLength = 128;

    [Required]
    [MaxLength(Recruiter.LoginMaxLength)]
    public string Login { get; init; } = "";

    [Required]
    [MaxLength(PasswordMaxLength)]
    public string Password { get; init; } = "";
}

public record RecruiterResponse(Guid Id, string Login);
