using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

public sealed class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable("candidate_profiles");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.AuthSubject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(160);
        builder.Property(x => x.PhoneNumber).HasMaxLength(40);
        builder.Property(x => x.Headline).HasMaxLength(160);
        builder.Property(x => x.Summary).HasMaxLength(4000);
        builder.Property(x => x.ResumeObjectKey).HasMaxLength(512);
        builder.Property(x => x.ResumeFileName).HasMaxLength(255);
        builder.Property(x => x.ResumeUploadedAtUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.AuthSubject }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Email });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
