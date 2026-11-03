using HireFlow.Domain.JobApplications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class ApplicationQuestionConfiguration : IEntityTypeConfiguration<ApplicationQuestion>
{
    public void Configure(EntityTypeBuilder<ApplicationQuestion> builder)
    {
        builder.ToTable("application_questions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Text).HasMaxLength(500);
        builder.Property(q => q.Criterion).HasMaxLength(1000);
        builder.Property(q => q.Answer).HasMaxLength(4000);
        builder.HasIndex(q => new { q.ApplicationId, q.Position }).IsUnique();
    }
}
