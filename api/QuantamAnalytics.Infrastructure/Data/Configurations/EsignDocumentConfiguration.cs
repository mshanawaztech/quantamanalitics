using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class EsignDocumentConfiguration : IEntityTypeConfiguration<EsignDocument>
{
    public void Configure(EntityTypeBuilder<EsignDocument> builder)
    {
        builder.ToTable("esign_documents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.CandidateProfileId).IsRequired();

        builder.Property(x => x.CandidateName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CandidateEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.SentByAuthSubject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.TemplateSlug)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ProviderSubmissionId)
            .HasMaxLength(64);

        builder.Property(x => x.SigningUrl)
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.SentAtUtc);
        builder.Property(x => x.ViewedAtUtc);
        builder.Property(x => x.SignedAtUtc);
        builder.Property(x => x.CancelledAtUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        // Recruiter list query: by tenant + status + recently sent.
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAtUtc });
        // Webhook lookup is by provider id (unique, partial — null while drafting).
        builder.HasIndex(x => x.ProviderSubmissionId)
            .IsUnique()
            .HasFilter("provider_submission_id IS NOT NULL");

        builder.HasOne<CandidateProfile>()
            .WithMany()
            .HasForeignKey(x => x.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
