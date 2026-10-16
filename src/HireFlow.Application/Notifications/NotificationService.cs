using HireFlow.Application.Common;
using HireFlow.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Application.Notifications;

public class NotificationService(IAppDbContext db) : INotificationService
{
    private const int BatchSize = 50;

    public async Task<List<DueNotificationResponse>> GetDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var query =
            from n in db.Notifications
            join a in db.JobApplications on n.ApplicationId equals a.Id
            join c in db.Candidates on a.CandidateId equals c.Id
            join v in db.Vacancies on a.VacancyId equals v.Id
            where n.Status == NotificationStatus.Pending && n.SendAfter <= now
            orderby n.SendAfter
            select new DueNotificationResponse(n.Id, c.TelegramId, n.Type, v.Title);

        return await query.Take(BatchSize).ToListAsync(ct);
    }

    public async Task MarkSentAsync(Guid id, CancellationToken ct)
    {
        var notification = await FindAsync(id, ct);
        notification.MarkSent();
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid id, NotificationFailedRequest request, CancellationToken ct)
    {
        var notification = await FindAsync(id, ct);
        notification.MarkFailed(request.Error);
        await db.SaveChangesAsync(ct);
    }

    // Ищет уведомление по id или бросает NotFoundException.
    private async Task<Notification> FindAsync(Guid id, CancellationToken ct)
    {
        return await db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new NotFoundException($"Notification {id} not found.");
    }
}
