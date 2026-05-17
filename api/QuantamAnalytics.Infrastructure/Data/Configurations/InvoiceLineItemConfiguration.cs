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

        builder.Property(x => x.Hours).HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Rate).HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(14, 2).IsRequired();

        builder.Property(x => x.SortOrder).IsRequired();

        // Stable display order for "show line items" queries.
        builder.HasIndex(x => new { x.InvoiceId, x.SortOrder });
    }
}
