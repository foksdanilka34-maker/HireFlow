using HireFlow.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    // Описывает, как Notification хранится в таблице notifications.
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(n => n.Type)
            .HasConversion(new SnakeCaseEnumConverter<NotificationType>())
            .HasMaxLength(20);
        builder.Property(n => n.Status)
            .HasConversion(new SnakeCaseEnumConverter<NotificationStatus>())
            .HasMaxLength(20);

        builder.Property(n => n.LastError).HasMaxLength(Notification.ErrorMaxLength);

        builder.HasIndex(n => new { n.Status, n.SendAfter });
    }
}
