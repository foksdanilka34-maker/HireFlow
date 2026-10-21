using HireFlow.Domain.Recruiters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class RecruiterConfiguration : IEntityTypeConfiguration<Recruiter>
{
    // Описывает, как Recruiter хранится в таблице recruiters.
    public void Configure(EntityTypeBuilder<Recruiter> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(r => r.Login).HasMaxLength(Recruiter.LoginMaxLength);
        builder.Property(r => r.PasswordHash).HasMaxLength(Recruiter.PasswordHashMaxLength);

        builder.HasIndex(r => r.Login).IsUnique();
    }
}
