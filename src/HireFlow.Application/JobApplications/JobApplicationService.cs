using HireFlow.Application.Common;
using HireFlow.Application.Notifications;
using HireFlow.Application.Resumes;
using HireFlow.Domain;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using HireFlow.Domain.Screening;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HireFlow.Application.JobApplications;

public class JobApplicationService(
    IAppDbContext db,
    IResumeReader resumeReader,
    IFileStorage fileStorage,
    IOptions<NotificationOptions> notificationOptions,
    HireFlowMetrics metrics)
    : IJobApplicationService
{
    private readonly TimeSpan _rejectionDelay = notificationOptions.Value.RejectionDelay;

    public async Task<ApplicationResponse> CreateAsync(CreateApplicationRequest request, CancellationToken ct)
    {
        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.TelegramId == request.TelegramId, ct);

        var vacancy = await db.Vacancies.FirstOrDefaultAsync(v => v.Id == request.VacancyId, ct)
            ?? throw new NotFoundException($"Vacancy {request.VacancyId} not found.");
        vacancy.EnsureAcceptsApplications();

        if (candidate is not null
            && await db.JobApplications.AnyAsync(a => a.CandidateId == candidate.Id && a.VacancyId == vacancy.Id, ct))
            throw new DomainException("Candidate has already applied to this vacancy.");

        var screeningStarted = Stopwatch.GetTimestamp();
        var resume = resumeReader.Read(request.FileContent);
        var screening = ResumeScorer.Evaluate(resume.Text, request.YearsOfExperience, vacancy.Requirements);
        var screeningDuration = Stopwatch.GetElapsedTime(screeningStarted);

        // Нового кандидата создаём вместе с откликом: имя из запроса, телефон — введённый или найденный в резюме.
        if (candidate is null)
        {
            candidate = CreateCandidate(request, resume.Text);
            db.Candidates.Add(candidate);
        }

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

        application.ApplyScreening(screening, _rejectionDelay);

        db.JobApplications.Add(application);
        await db.SaveChangesAsync(ct);
        metrics.ScreeningDuration(screeningDuration);
        metrics.ApplicationCreated(screening.Verdict);

        return ApplicationResponse.From(application, vacancy, candidate, new Dictionary<Guid, string>(), rejectionSent: false);
    }

    // Собирает нового кандидата из запроса и резюме; нет имени или телефона — BadRequestException.
    private static Candidate CreateCandidate(CreateApplicationRequest request, string resumeText)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
            throw new BadRequestException("Name is required for a new candidate.");
        if (name.Length > Candidate.NameMaxLength)
            throw new BadRequestException($"Name must not exceed {Candidate.NameMaxLength} characters.");

        string phone;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            if (!PhoneNumber.TryNormalize(request.Phone, out phone))
                throw new BadRequestException("Phone number is not valid.");
        }
        else
        {
            phone = ResumeFacts.FindPhone(resumeText)
                ?? throw new BadRequestException("Phone number is required: it was not found in the resume.");
        }

        return new Candidate(request.TelegramId, name, phone);
    }

    public async Task<ApplicationResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var application = await db.JobApplications
            .AsNoTracking()
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Application {id} not found.");

        return await ToResponseAsync(application, ct);
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

    public async Task<ApplicationResponse> DecideAsync(Guid id, Guid recruiterId, DecisionRequest request, CancellationToken ct)
    {
        var application = await db.JobApplications
            .Include(a => a.History)
            .Include(a => a.Notifications)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Application {id} not found.");

        var fromStatus = application.Status;
        if (request.Decision == RecruiterDecision.Invite)
            application.Invite(recruiterId, request.Comment);
        else
            application.Reject(recruiterId, request.Comment, _rejectionDelay);

        await db.SaveChangesAsync(ct);
        metrics.Decision(request.Decision ?? RecruiterDecision.Reject, fromStatus);
        return await ToResponseAsync(application, ct);
    }

    public async Task<ResumeFile> GetResumeAsync(Guid id, CancellationToken ct)
    {
        var file = await db.JobApplications
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.ResumeFileKey, a.ResumeFileName })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Application {id} not found.");

        var content = await fileStorage.OpenReadAsync(file.ResumeFileKey, ct)
            ?? throw new NotFoundException($"Resume file of application {id} not found.");

        var contentType = Path.GetExtension(file.ResumeFileKey).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream",
        };
        return new ResumeFile(content, file.ResumeFileName, contentType);
    }

    public async Task<ResumeTextResponse> GetResumeTextAsync(Guid id, CancellationToken ct)
    {
        var text = await db.JobApplications
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => a.ResumeText)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Application {id} not found.");

        return new ResumeTextResponse(text);
    }

    // Догружает вакансию, кандидата и логины рекрутеров из истории (одним запросом) и собирает карточку.
    private async Task<ApplicationResponse> ToResponseAsync(JobApplication application, CancellationToken ct)
    {
        var vacancy = await db.Vacancies.AsNoTracking().FirstAsync(v => v.Id == application.VacancyId, ct);
        var candidate = await db.Candidates.AsNoTracking().FirstAsync(c => c.Id == application.CandidateId, ct);

        var recruiterIds = application.History
            .Where(h => h.RecruiterId is not null)
            .Select(h => h.RecruiterId!.Value)
            .Distinct()
            .ToList();
        var logins = recruiterIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Recruiters.AsNoTracking()
                .Where(r => recruiterIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Login, ct);

        var rejectionSent = await db.Notifications.AnyAsync(n => n.ApplicationId == application.Id
            && n.Type == NotificationType.Rejected && n.Status == NotificationStatus.Sent, ct);

        return ApplicationResponse.From(application, vacancy, candidate, logins, rejectionSent);
    }
}
