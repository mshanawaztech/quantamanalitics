using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class BackgroundCheckConfiguration : IEntityTypeConfiguration<BackgroundCheck>
{
    public void Configure(EntityTypeBuilder<BackgroundCheck> builder)
    {
        builder.ToTable("background_checks");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.CandidateProfileId).IsRequired();

        builder.Property(x => x.CandidateName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CandidateEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.RequestedByAuthSubject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.PackageSlug)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ProviderReportId)
            .HasMaxLength(64);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.StatusDetail)
            .HasMaxLength(500);

        builder.Property(x => x.RequestedAtUtc).IsRequired();
        builder.Property(x => x.CompletedAtUtc);
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        // Tenant-scoped lookups: list by tenant + status (recruiter dashboard),
        // and webhook lookup by provider report id (still tenant-scoped via
        // the global query filter on AppDbContext).
        builder.HasIndex(x => new { x.TenantId, x.Status, x.RequestedAtUtc });
        builder.HasIndex(x => x.ProviderReportId)
            .IsUnique()
            .HasFilter("provider_report_id IS NOT NULL");

        builder.HasOne<CandidateProfile>()
            .WithMany()
            .HasForeignKey(x => x.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
