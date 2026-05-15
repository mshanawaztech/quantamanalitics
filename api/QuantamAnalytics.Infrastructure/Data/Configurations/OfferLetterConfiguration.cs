using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class OfferLetterConfiguration : IEntityTypeConfiguration<OfferLetter>
{
    public void Configure(EntityTypeBuilder<OfferLetter> builder)
    {
        builder.ToTable("offer_letters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.CandidateProfileId).IsRequired();
        builder.Property(x => x.ApplicationId);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BaseAnnualSalary).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.BodyMarkdown).IsRequired();
        builder.Property(x => x.CreatedByAuthSubject).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.SentAtUtc);
        builder.Property(x => x.ResolvedAtUtc);
        builder.Property(x => x.CandidateResponseNote).HasMaxLength(2000);

        builder.HasIndex(x => new { x.TenantId, x.CandidateProfileId })
            .HasDatabaseName("ix_offer_letters_tenant_candidate");
        builder.HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_offer_letters_tenant_status");
    }
}
