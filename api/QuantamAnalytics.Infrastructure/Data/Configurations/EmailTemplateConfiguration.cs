using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("email_templates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();

        builder.Property(x => x.Slug)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasMaxLength(300)
            .IsRequired();

        // Body is unbounded — templates can run long and arbitrary caps
        // cause more support tickets than they save. Postgres `text` is
        // the same on-disk shape as a varchar with no length cap.
        builder.Property(x => x.BodyMarkdown)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.CreatedByAuthSubject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        // Plain tenant index — most lookups filter by tenant first.
        builder.HasIndex(x => x.TenantId);

        // Slug is the stable handle code uses to fetch a template by name.
        // Unique per tenant so two tenants can both own an
        // "interview-invite" template, but the same tenant cannot.
        builder.HasIndex(x => new { x.TenantId, x.Slug })
            .IsUnique()
            .HasDatabaseName("ix_email_templates_tenant_slug_unique");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
