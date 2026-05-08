using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();

        builder.Property(x => x.ContractorAuthSubject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ContractorEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.PeriodStartUtc).IsRequired();
        builder.Property(x => x.PeriodEndUtc).IsRequired();

        builder.Property(x => x.Hours)
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.Notes).HasMaxLength(2000);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.ReviewedByAuthSubject).HasMaxLength(255);
        builder.Property(x => x.ReviewerNote).HasMaxLength(1000);

        builder.Property(x => x.SubmittedAtUtc);
        builder.Property(x => x.ReviewedAtUtc);
        builder.Property(x => x.PaidAtUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        // Most common queries:
        // - "show me my invoices" — by tenant + contractor + recency
        builder.HasIndex(x => new { x.TenantId, x.ContractorAuthSubject, x.PeriodStartUtc });
        // - "show me what's pending review at this tenant" — by tenant + status
        builder.HasIndex(x => new { x.TenantId, x.Status });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
