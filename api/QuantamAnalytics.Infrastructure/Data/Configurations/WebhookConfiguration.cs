using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.TargetUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.EventTypes).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.SecretHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SecretPrefix).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedByAuthSubject).HasMaxLength(255).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.LastDeliveryAttemptUtc);
        builder.Property(x => x.LastSuccessfulDeliveryUtc);
        builder.Property(x => x.ConsecutiveFailureCount).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.IsActive })
            .HasDatabaseName("ix_webhook_subscriptions_tenant_active");
    }
}

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.SubscriptionId).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.NextAttemptAtUtc).IsRequired();
        builder.Property(x => x.LastAttemptedAtUtc);
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.LastResponseStatusCode);
        builder.Property(x => x.LastResponseBody).HasMaxLength(2000);

        // Hot path: dequeue Pending rows ready to attempt, ordered by oldest.
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc })
            .HasDatabaseName("ix_webhook_deliveries_status_next_attempt");
        builder.HasIndex(x => new { x.TenantId, x.CreatedAtUtc })
            .HasDatabaseName("ix_webhook_deliveries_tenant_created");
    }
}
