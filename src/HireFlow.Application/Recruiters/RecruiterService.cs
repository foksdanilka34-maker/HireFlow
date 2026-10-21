using HireFlow.Application.Common;
using HireFlow.Domain;
using HireFlow.Domain.Recruiters;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Application.Recruiters;

public class RecruiterService(IAppDbContext db, IPasswordHasher passwordHasher) : IRecruiterService
{
    public const int PasswordMinLength = 8;

    // Фиктивный хэш: проверяется, когда логина нет, чтобы время ответа не выдавало существование логина.
    private static string? _dummyHash;

    public async Task<RecruiterResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        Recruiter? recruiter = null;
        try
        {
            var login = Recruiter.NormalizeLogin(request.Login);
            recruiter = await db.Recruiters.AsNoTracking().FirstOrDefaultAsync(r => r.Login == login, ct);
        }
        catch (DomainException)
        {
            // Логин недопустимого вида — такого рекрутера точно нет.
        }

        if (recruiter is null)
        {
            _dummyHash ??= passwordHasher.Hash("dummy-password-for-timing");
            passwordHasher.Verify(_dummyHash, request.Password);
            return null;
        }

        return passwordHasher.Verify(recruiter.PasswordHash, request.Password)
            ? new RecruiterResponse(recruiter.Id, recruiter.Login)
            : null;
    }

    public async Task<RecruiterResponse> SetPasswordAsync(string login, string password, CancellationToken ct)
    {
        var normalized = Recruiter.NormalizeLogin(login);
        if (password.Length is < PasswordMinLength or > LoginRequest.PasswordMaxLength)
            throw new BadRequestException(
                $"Password must be from {PasswordMinLength} to {LoginRequest.PasswordMaxLength} characters.");

        var hash = passwordHasher.Hash(password);
        var recruiter = await db.Recruiters.FirstOrDefaultAsync(r => r.Login == normalized, ct);
        if (recruiter is null)
        {
            recruiter = new Recruiter(normalized, hash);
            db.Recruiters.Add(recruiter);
        }
        else
        {
            recruiter.ChangePasswordHash(hash);
        }

        await db.SaveChangesAsync(ct);
        return new RecruiterResponse(recruiter.Id, recruiter.Login);
    }
}
