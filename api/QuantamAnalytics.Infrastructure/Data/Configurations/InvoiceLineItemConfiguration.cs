using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("invoice_line_items");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.InvoiceId).IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsRequired();

        // v4 — week-based engagement fields. Nullable for pre-v4 rows the
        // migration backfills with NULL week range + DaysWorked=0,
        // HoursPerDay=Hours so totals reconcile.
        builder.Property(x => x.WeekStartUtc);
        builder.Property(x => x.WeekEndUtc);
        builder.Property(x => x.DaysWorked).HasPrecision(6, 2).IsRequired();
        builder.Property(x => x.HoursPerDay).HasPrecision(6, 2).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.Property(x => x.Hours).HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Rate).HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(14, 2).IsRequired();

        builder.Property(x => x.SortOrder).IsRequired();

        // Stable display order for "show line items" queries.
        builder.HasIndex(x => new { x.InvoiceId, x.SortOrder });
        // Date-range queries (e.g., "which invoice covers week of 5/4?").
        builder.HasIndex(x => new { x.InvoiceId, x.WeekStartUtc });
    }
}
