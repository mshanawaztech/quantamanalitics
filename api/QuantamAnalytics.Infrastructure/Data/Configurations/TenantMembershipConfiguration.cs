using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.AuthSubject).HasMaxLength(255).IsRequired();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.JoinedAtUtc).IsRequired();

        // One membership per user — uniqueness enforced at the DB level so
        // the bootstrap endpoint can rely on a 23505 to detect re-joins.
        builder.HasIndex(x => x.AuthSubject).IsUnique()
            .HasDatabaseName("ix_tenant_memberships_auth_subject_unique");
        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_tenant_memberships_tenant");
    }
}
