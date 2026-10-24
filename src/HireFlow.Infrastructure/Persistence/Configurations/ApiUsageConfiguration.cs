using HireFlow.Domain.ApiClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class ApiUsageConfiguration : IEntityTypeConfiguration<ApiUsage>
{
    // Описывает, как ApiUsage хранится в таблице api_usages: одна строка на клиента и день.
    public void Configure(EntityTypeBuilder<ApiUsage> builder)
    {
        builder.HasKey(u => new { u.ApiClientId, u.Day });

        builder.HasOne<ApiClient>()
            .WithMany()
            .HasForeignKey(u => u.ApiClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
