using HireFlow.Application.Common;
using HireFlow.Domain.BotSessions;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using HireFlow.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<BotSession> BotSessions => Set<BotSession>();

    // Подключает все конфигурации сущностей из папки Configurations.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
