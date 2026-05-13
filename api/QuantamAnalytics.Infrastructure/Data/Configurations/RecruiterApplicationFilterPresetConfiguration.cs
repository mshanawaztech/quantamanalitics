using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class RecruiterApplicationFilterPresetConfiguration : IEntityTypeConfiguration<RecruiterApplicationFilterPreset>
{
    public void Configure(EntityTypeBuilder<RecruiterApplicationFilterPreset> builder)
    {
        builder.ToTable("recruiter_application_filter_presets");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.CreatedByAuthSubject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Search).HasMaxLength(160);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.Tag).HasMaxLength(64);
        builder.Property(x => x.Location).HasMaxLength(160);
        builder.Property(x => x.StuckOnly).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.CreatedByAuthSubject, x.Name }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
