using HireFlow.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    // Описывает, как Candidate хранится в таблице candidates.
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(c => c.Name).HasMaxLength(Candidate.NameMaxLength);
        builder.Property(c => c.Phone).HasMaxLength(Candidate.PhoneMaxLength);

        builder.HasIndex(c => c.TelegramId).IsUnique();
        builder.HasIndex(c => c.Phone);
    }
}
