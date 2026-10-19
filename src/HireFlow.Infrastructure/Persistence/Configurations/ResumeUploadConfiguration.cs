using HireFlow.Domain.Resumes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class ResumeUploadConfiguration : IEntityTypeConfiguration<ResumeUpload>
{
    // Описывает, как ResumeUpload хранится в таблице resume_uploads.
    public void Configure(EntityTypeBuilder<ResumeUpload> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(u => u.FileHash).HasMaxLength(ResumeUpload.FileHashLength);

        builder.HasIndex(u => new { u.TelegramId, u.FileHash }).IsUnique();
    }
}
