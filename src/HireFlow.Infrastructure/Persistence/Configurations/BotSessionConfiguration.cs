using HireFlow.Domain.BotSessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class BotSessionConfiguration : IEntityTypeConfiguration<BotSession>
{
    // Описывает, как BotSession хранится в таблице bot_sessions.
    public void Configure(EntityTypeBuilder<BotSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(s => s.Step).HasMaxLength(BotSession.StepMaxLength);
        builder.Property(s => s.Draft).HasColumnType("jsonb");

        builder.HasIndex(s => s.TelegramId).IsUnique();
    }
}
