using HireFlow.Application.Common;
using HireFlow.Domain.BotSessions;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using HireFlow.Domain.Resumes;
using HireFlow.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HireFlow.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<BotSession> BotSessions => Set<BotSession>();
    public DbSet<ResumeUpload> ResumeUploads => Set<ResumeUpload>();

    // Сохраняет изменения; нарушение уникального индекса (гонка двух запросов) превращает в ConflictException.
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await base.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("The record already exists.", ex);
        }
    }

    // Подключает все конфигурации сущностей из папки Configurations.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
