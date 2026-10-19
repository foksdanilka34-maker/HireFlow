using HireFlow.Domain.BotSessions;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using HireFlow.Domain.Recruiters;
using HireFlow.Domain.Resumes;
using HireFlow.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Application.Common;

public interface IAppDbContext
{
    DbSet<Vacancy> Vacancies { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<JobApplication> JobApplications { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<BotSession> BotSessions { get; }
    DbSet<ResumeUpload> ResumeUploads { get; }
    DbSet<Recruiter> Recruiters { get; }

    // Сохраняет все изменения в базу одной транзакцией.
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
