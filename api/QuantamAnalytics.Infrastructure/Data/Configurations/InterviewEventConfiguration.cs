using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class InterviewEventConfiguration : IEntityTypeConfiguration<InterviewEvent>
{
    public void Configure(EntityTypeBuilder<InterviewEvent> builder)
    {
        builder.ToTable("interview_events");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.SubmissionId).IsRequired();

        builder.Property(x => x.CandidateName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CandidateEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.InterviewerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ScheduledStartUtc).IsRequired();
        builder.Property(x => x.ScheduledEndUtc).IsRequired();

        builder.Property(x => x.ExternalEventId)
            .HasMaxLength(200);

        builder.Property(x => x.MeetingJoinUrl)
            .HasMaxLength(500);

        builder.Property(x => x.CancelledAtUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.SubmissionId });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.ScheduledStartUtc });

        builder.HasOne<Submission>()
            .WithMany()
            .HasForeignKey(x => x.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
