using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("tenant_branding");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TenantId).IsRequired();

        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.LegalName).HasMaxLength(200);
        builder.Property(x => x.ContactEmail).HasMaxLength(320);
        builder.Property(x => x.ContactPhone).HasMaxLength(64);

        builder.Property(x => x.AddressLine1).HasMaxLength(200);
        builder.Property(x => x.AddressLine2).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.StateRegion).HasMaxLength(100);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.Country).HasMaxLength(100);

        builder.Property(x => x.BankName).HasMaxLength(200);
        // Bank account / routing numbers stay as strings (not ints) so leading
        // zeros and IBAN-style alphanumerics still render correctly.
        builder.Property(x => x.BankAccountNumber).HasMaxLength(64);
        builder.Property(x => x.BankRoutingNumber).HasMaxLength(64);

        builder.Property(x => x.DefaultHourlyRate).HasPrecision(14, 4);
        builder.Property(x => x.DefaultCurrency).HasMaxLength(3);
        builder.Property(x => x.DefaultPaymentTermsDays);

        builder.Property(x => x.PrimaryColorHex).HasMaxLength(9);
        builder.Property(x => x.AccentColorHex).HasMaxLength(9);
        builder.Property(x => x.LogoObjectKey).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        // One branding row per tenant. The unique index is the source of
        // truth for that invariant; the application code guards it too.
        builder.HasIndex(x => x.TenantId).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
