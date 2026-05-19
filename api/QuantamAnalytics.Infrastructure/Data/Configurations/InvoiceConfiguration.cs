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

        // v2 — human-readable number, per-tenant unique
        builder.Property(x => x.InvoiceNumber)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.ClientName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.IssueDateUtc).IsRequired();
        builder.Property(x => x.DueDateUtc).IsRequired();
        builder.Property(x => x.PeriodStartUtc).IsRequired();
        builder.Property(x => x.PeriodEndUtc).IsRequired();

        builder.Property(x => x.Hours)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(x => x.Subtotal)
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(x => x.TaxRate)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.TaxAmount)
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.Notes).HasMaxLength(2000);

        // qa005 — optional per-invoice Remit-to overrides. NULL means
        // "fall back to TenantBranding" at PDF render time. Lengths
        // mirror the branding entity so a value valid in one is valid
        // in the other.
        builder.Property(x => x.RemitBankName).HasMaxLength(120);
        builder.Property(x => x.RemitAccountNumber).HasMaxLength(40);
        builder.Property(x => x.RemitRoutingNumber).HasMaxLength(40);
        builder.Property(x => x.RemitContactPhone).HasMaxLength(40);

        // Vendor reference — denormalized free-text sub-department label.
        builder.Property(x => x.VendorName).HasMaxLength(255);

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

        // Child collection. Cascade-delete with the parent — line items are
        // never reachable across tenants; the invoice already enforces the
        // tenant filter, so the children inherit isolation for free.
        builder.HasMany(x => x.LineItems)
            .WithOne()
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Most common queries:
        // - "show me my invoices" — by tenant + contractor + recency
        builder.HasIndex(x => new { x.TenantId, x.ContractorAuthSubject, x.IssueDateUtc });
        // - "show me what's pending review at this tenant" — by tenant + status
        builder.HasIndex(x => new { x.TenantId, x.Status });
        // - "look up by invoice number" — per-tenant unique
        builder.HasIndex(x => new { x.TenantId, x.InvoiceNumber }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
