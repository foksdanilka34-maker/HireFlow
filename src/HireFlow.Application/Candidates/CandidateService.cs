using HireFlow.Application.Common;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Application.Candidates;

public class CandidateService(IAppDbContext db) : ICandidateService
{
    public async Task<CandidateResponse> GetAsync(long telegramId, CancellationToken ct)
    {
        var candidate = await db.Candidates.AsNoTracking().FirstOrDefaultAsync(c => c.TelegramId == telegramId, ct)
            ?? throw new NotFoundException($"Candidate with Telegram ID {telegramId} not found.");

        return CandidateResponse.From(candidate);
    }

    public async Task<CandidateResponse> UpsertAsync(long telegramId, CandidateRequest request, CancellationToken ct)
    {
        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.TelegramId == telegramId, ct);

        if (candidate is null)
        {
            candidate = new Candidate(telegramId, request.Name, request.Phone);
            db.Candidates.Add(candidate);
        }
        else
        {
            candidate.Update(request.Name, request.Phone);
        }

        await db.SaveChangesAsync(ct);
        return CandidateResponse.From(candidate);
    }

    public async Task<List<CandidateApplicationResponse>> GetApplicationsAsync(long telegramId, CancellationToken ct)
    {
        var query =
            from a in db.JobApplications
            join c in db.Candidates on a.CandidateId equals c.Id
            join v in db.Vacancies on a.VacancyId equals v.Id
            where c.TelegramId == telegramId
            orderby a.CreatedAt descending
            select new
            {
                a.Id,
                a.VacancyId,
                v.Title,
                a.Status,
                RejectionSent = a.Notifications.Any(n =>
                    n.Type == NotificationType.Rejected && n.Status == NotificationStatus.Sent),
                a.CreatedAt,
            };

        var applications = await query.ToListAsync(ct);

        return applications
            .Select(a => new CandidateApplicationResponse(
                a.Id, a.VacancyId, a.Title, ToCandidateStatus(a.Status, a.RejectionSent), a.CreatedAt))
            .ToList();
    }

    // Переводит внутренний статус в статус для кандидата. Об отказе кандидат узнаёт только после уведомления.
    private static CandidateStatus ToCandidateStatus(ApplicationStatus status, bool rejectionSent) => status switch
    {
        ApplicationStatus.Invited => CandidateStatus.Invited,
        ApplicationStatus.Rejected or ApplicationStatus.AutoRejected when rejectionSent => CandidateStatus.Rejected,
        _ => CandidateStatus.InReview,
    };
}
