using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.Slug)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.Location)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.Summary)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(x => x.PostedOnUtc).IsRequired();

        builder.Property(x => x.IsPublished)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.DeletedAtUtc);

        builder.Property(x => x.DeletedByAuthSubject)
            .HasMaxLength(255);

        builder.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();

        // Hot path for the recycle bin: list-deleted-for-tenant ordered by
        // DeletedAtUtc desc. Partial index keeps the active-jobs path cheap.
        builder.HasIndex(x => new { x.TenantId, x.DeletedAtUtc })
            .HasFilter("is_deleted = true")
            .HasDatabaseName("ix_jobs_tenant_deleted");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
