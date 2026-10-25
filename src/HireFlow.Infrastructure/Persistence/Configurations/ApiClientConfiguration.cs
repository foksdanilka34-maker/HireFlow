using HireFlow.Domain.ApiClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class ApiClientConfiguration : IEntityTypeConfiguration<ApiClient>
{
    // Описывает, как ApiClient хранится в таблице api_clients.
    public void Configure(EntityTypeBuilder<ApiClient> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(c => c.Name).HasMaxLength(ApiClient.NameMaxLength);
        builder.Property(c => c.KeyPrefix).HasMaxLength(ApiClient.KeyPrefixLength);
        builder.Property(c => c.KeyHash).HasMaxLength(ApiClient.KeyHashLength);
        builder.Ignore(c => c.IsActive);

        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasIndex(c => c.KeyHash).IsUnique();
    }
}
