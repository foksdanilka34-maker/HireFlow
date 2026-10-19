using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Recruiters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class ApplicationStatusChangeConfiguration : IEntityTypeConfiguration<ApplicationStatusChange>
{
    // Описывает, как запись истории статусов хранится в таблице application_status_history.
    public void Configure(EntityTypeBuilder<ApplicationStatusChange> builder)
    {
        builder.ToTable("application_status_history");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(h => h.FromStatus)
            .HasConversion(new SnakeCaseEnumConverter<ApplicationStatus>())
            .HasMaxLength(30);
        builder.Property(h => h.ToStatus)
            .HasConversion(new SnakeCaseEnumConverter<ApplicationStatus>())
            .HasMaxLength(30);
        builder.Property(h => h.ChangedBy)
            .HasConversion(new SnakeCaseEnumConverter<StatusChangedBy>())
            .HasMaxLength(20);

        builder.Property(h => h.Reason).HasMaxLength(ApplicationStatusChange.ReasonMaxLength);

        builder.HasOne<Recruiter>()
            .WithMany()
            .HasForeignKey(h => h.RecruiterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
