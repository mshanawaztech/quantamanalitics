using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class TimesheetConfiguration : IEntityTypeConfiguration<Timesheet>
{
    public void Configure(EntityTypeBuilder<Timesheet> builder)
    {
        builder.ToTable("timesheets");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();

        builder.Property(x => x.ContractorAuthSubject)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ContractorEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.WeekStartUtc).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ReviewedByAuthSubject)
            .HasMaxLength(200);

        builder.Property(x => x.ReviewNote)
            .HasMaxLength(1000);

        builder.Property(x => x.SubmittedAtUtc);
        builder.Property(x => x.ReviewedAtUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.ContractorAuthSubject, x.WeekStartUtc }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status });

        builder.HasMany(x => x.Entries)
            .WithOne()
            .HasForeignKey(x => x.TimesheetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Entries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
