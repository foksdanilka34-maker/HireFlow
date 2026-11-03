using HireFlow.Domain.Ai;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class AiReviewJobConfiguration : IEntityTypeConfiguration<AiReviewJob>
{
    public void Configure(EntityTypeBuilder<AiReviewJob> builder)
    {
        builder.ToTable("ai_review_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Status).HasConversion(new SnakeCaseEnumConverter<AiReviewJobStatus>()).HasMaxLength(20);
        builder.Property(j => j.Kind).HasConversion(new SnakeCaseEnumConverter<AiReviewJobKind>()).HasMaxLength(20).HasDefaultValue(AiReviewJobKind.Resume);
        builder.Property(j => j.LastError).HasMaxLength(300);
        builder.Property(j => j.RequirementsSnapshot).HasColumnType("jsonb");
        builder.HasIndex(j => new { j.ApplicationId, j.Kind }).IsUnique();
        builder.HasIndex(j => new { j.Status, j.AvailableAt });
        builder.HasOne(j => j.Application).WithMany().HasForeignKey(j => j.ApplicationId).OnDelete(DeleteBehavior.Cascade);
    }
}
