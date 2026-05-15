using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class PlatformApiKeyConfiguration : IEntityTypeConfiguration<PlatformApiKey>
{
    public void Configure(EntityTypeBuilder<PlatformApiKey> builder)
    {
        builder.ToTable("platform_api_keys");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(120).IsRequired();
        builder.Property(x => x.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.KeyPrefix).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedByAuthSubject).HasMaxLength(255).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.LastUsedAtUtc);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.KeyHash)
            .IsUnique()
            .HasDatabaseName("ix_platform_api_keys_key_hash");
        builder.HasIndex(x => new { x.TenantId, x.IsActive })
            .HasDatabaseName("ix_platform_api_keys_tenant_active");
    }
}
