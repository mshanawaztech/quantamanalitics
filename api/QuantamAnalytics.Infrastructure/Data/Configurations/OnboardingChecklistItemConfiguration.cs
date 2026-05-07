using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class OnboardingChecklistItemConfiguration : IEntityTypeConfiguration<OnboardingChecklistItem>
{
    public void Configure(EntityTypeBuilder<OnboardingChecklistItem> builder)
    {
        builder.ToTable("onboarding_checklist_items");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.CandidateProfileId).IsRequired();

        builder.Property(x => x.AssignedByAuthSubject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ItemType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Instructions)
            .HasMaxLength(2000);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CandidateNote)
            .HasMaxLength(1000);

        builder.Property(x => x.ReviewerNote)
            .HasMaxLength(1000);

        builder.Property(x => x.AssignedAtUtc).IsRequired();
        builder.Property(x => x.SubmittedAtUtc);
        builder.Property(x => x.ReviewedAtUtc);
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        // Recruiter list query: by tenant + candidate, ordered by assignment.
        builder.HasIndex(x => new { x.TenantId, x.CandidateProfileId, x.AssignedAtUtc });
        // Recruiter dashboard "what's pending review" query.
        builder.HasIndex(x => new { x.TenantId, x.Status });

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
