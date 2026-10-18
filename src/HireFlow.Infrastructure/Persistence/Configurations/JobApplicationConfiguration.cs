using HireFlow.Domain.Candidates;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    // Описывает, как JobApplication хранится в таблице applications.
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("applications");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("uuidv7()");

        // Токен оптимистичной блокировки на системной колонке PostgreSQL xmin (в таблице не создаётся).
        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasOne<Vacancy>().WithMany().HasForeignKey(a => a.VacancyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Candidate>().WithMany().HasForeignKey(a => a.CandidateId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.ResumeFileKey).HasMaxLength(JobApplication.FileKeyMaxLength);
        builder.Property(a => a.ResumeFileName).HasMaxLength(JobApplication.FileNameMaxLength);
        builder.Property(a => a.ResumeFileHash).HasMaxLength(JobApplication.FileHashLength);

        builder.Property(a => a.Status)
            .HasConversion(new SnakeCaseEnumConverter<ApplicationStatus>())
            .HasMaxLength(30);

        builder.ComplexProperty(a => a.Screening, screening =>
        {
            screening.ToJson();

            screening.Property(s => s.Score).HasJsonPropertyName("score");
            screening.Property(s => s.Verdict)
                .HasConversion(new SnakeCaseEnumConverter<ScreeningVerdict>())
                .HasJsonPropertyName("verdict");
            screening.PrimitiveCollection(s => s.MissingRequiredSkills).HasJsonPropertyName("missing_required_skills");
            screening.PrimitiveCollection(s => s.MissingNiceToHaveSkills).HasJsonPropertyName("missing_nice_to_have_skills");
            screening.Property(s => s.ExperienceIsEnough).HasJsonPropertyName("experience_is_enough");
        });

        builder.HasMany(a => a.History)
            .WithOne()
            .HasForeignKey(h => h.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Notifications)
            .WithOne()
            .HasForeignKey(n => n.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
