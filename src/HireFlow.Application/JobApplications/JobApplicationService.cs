using HireFlow.Application.Common;
using HireFlow.Application.Notifications;
using HireFlow.Application.Resumes;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Screening;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HireFlow.Application.JobApplications;

public class JobApplicationService(
    IAppDbContext db,
    IResumeReader resumeReader,
    IFileStorage fileStorage,
    IOptions<NotificationOptions> notificationOptions)
    : IJobApplicationService
{
    private readonly TimeSpan _rejectionDelay = notificationOptions.Value.RejectionDelay;

    public async Task<ApplicationResponse> CreateAsync(CreateApplicationRequest request, CancellationToken ct)
    {
        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.TelegramId == request.TelegramId, ct)
            ?? throw new NotFoundException($"Candidate with Telegram ID {request.TelegramId} not found.");

        var vacancy = await db.Vacancies.FirstOrDefaultAsync(v => v.Id == request.VacancyId, ct)
            ?? throw new NotFoundException($"Vacancy {request.VacancyId} not found.");
        vacancy.EnsureAcceptsApplications();

        var resume = resumeReader.Read(request.FileContent);
        var extension = resume.Format.ToString().ToLowerInvariant();
        var fileKey = await fileStorage.SaveAsync(request.FileContent, extension, ct);

        var application = new JobApplication(
            vacancy.Id,
            candidate.Id,
            request.YearsOfExperience,
            fileKey,
            Path.GetFileName(request.FileName),
            resume.Hash,
            resume.Text);

        var screening = ResumeScorer.Evaluate(resume.Text, request.YearsOfExperience, vacancy.Requirements);
        application.ApplyScreening(screening, _rejectionDelay);

        db.JobApplications.Add(application);
        await db.SaveChangesAsync(ct);

        return ApplicationResponse.From(application);
    }

    public async Task<ApplicationResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var application = await db.JobApplications
            .AsNoTracking()
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Application {id} not found.");

        return ApplicationResponse.From(application);
    }

    public async Task<List<ApplicationListItem>> ListAsync(Guid? vacancyId, ApplicationStatus? status, CancellationToken ct)
    {
        var applications = db.JobApplications.AsNoTracking();

        if (vacancyId is not null)
            applications = applications.Where(a => a.VacancyId == vacancyId);
        if (status is not null)
            applications = applications.Where(a => a.Status == status);

        var query =
            from a in applications
            join v in db.Vacancies on a.VacancyId equals v.Id
            join c in db.Candidates on a.CandidateId equals c.Id
            orderby a.CreatedAt descending
            select new ApplicationListItem(
                a.Id, a.VacancyId, v.Title, a.CandidateId, c.Name, a.Status,
                a.Screening.Score, a.Screening.Verdict, a.CreatedAt);

        return await query.ToListAsync(ct);
    }

    public async Task<ApplicationResponse> DecideAsync(Guid id, DecisionRequest request, CancellationToken ct)
    {
        var application = await db.JobApplications
            .Include(a => a.History)
            .Include(a => a.Notifications)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Application {id} not found.");

        if (request.Decision == RecruiterDecision.Invite)
            application.Invite(request.Comment);
        else
            application.Reject(request.Comment, _rejectionDelay);

        await db.SaveChangesAsync(ct);
        return ApplicationResponse.From(application);
    }
}
