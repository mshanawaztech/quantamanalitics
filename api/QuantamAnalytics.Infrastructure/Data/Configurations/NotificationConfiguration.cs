using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

/// <summary>
/// Maps the <see cref="Notification"/> aggregate to the <c>notifications</c>
/// table. Hot path is "all unread for a recipient" so the composite index
/// (TenantId, RecipientAuthSubject, ReadAtUtc) keeps it cheap.
/// </summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.TenantId).IsRequired();

        builder.Property(n => n.RecipientAuthSubject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(n => n.Kind)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(n => n.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.Body)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(n => n.TargetUrl)
            .HasMaxLength(500);

        builder.Property(n => n.CreatedAtUtc).IsRequired();

        builder.HasIndex(n => new { n.TenantId, n.RecipientAuthSubject, n.ReadAtUtc })
            .HasDatabaseName("ix_notifications_tenant_recipient_read");

        builder.HasIndex(n => new { n.TenantId, n.RecipientAuthSubject, n.CreatedAtUtc })
            .HasDatabaseName("ix_notifications_tenant_recipient_created");
    }
}
