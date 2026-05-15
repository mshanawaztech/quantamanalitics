using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class TenantSubscriptionConfiguration
    : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("tenant_subscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.PlanCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.IncludedSeats).IsRequired();
        builder.Property(x => x.PricePerSeat).HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.TrialEndsAtUtc);
        builder.Property(x => x.CurrentPeriodEndsAtUtc);
        builder.Property(x => x.StripeCustomerId).HasMaxLength(120);
        builder.Property(x => x.StripeSubscriptionId).HasMaxLength(120);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        // One subscription per tenant. The Stripe sync flow assumes this.
        builder.HasIndex(x => x.TenantId).IsUnique()
            .HasDatabaseName("ix_tenant_subscriptions_tenant_unique");
        builder.HasIndex(x => x.StripeSubscriptionId)
            .HasDatabaseName("ix_tenant_subscriptions_stripe_sub");
    }
}
