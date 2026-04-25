using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

/// <summary>
/// Maps the <see cref="Tenant"/> aggregate to the <c>tenants</c> table.
/// Slug is the only natural key — unique and immutable.
/// </summary>
internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        // Guid v7 is generated in the entity ctor — EF Core must not try to
        // overwrite it or treat the column as DB-generated.
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Slug)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.CreatedAtUtc).IsRequired();

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
