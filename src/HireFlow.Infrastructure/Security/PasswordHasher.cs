using HireFlow.Application.Recruiters;
using HireFlow.Domain.Recruiters;
using Microsoft.AspNetCore.Identity;

namespace HireFlow.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<Recruiter> _hasher = new();

    // Считает хэш пароля (PBKDF2 из ASP.NET Core Identity).
    public string Hash(string password) => _hasher.HashPassword(null!, password);

    // Проверяет пароль по сохранённому хэшу.
    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
}
