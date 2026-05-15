using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Data.Configurations;

internal sealed class InterviewerAvailabilityConfiguration
    : IEntityTypeConfiguration<InterviewerAvailability>
{
    public void Configure(EntityTypeBuilder<InterviewerAvailability> builder)
    {
        builder.ToTable("interviewer_availability");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.InterviewerAuthSubject)
            .HasMaxLength(255).IsRequired();
        builder.Property(x => x.DayOfWeek).IsRequired();
        builder.Property(x => x.StartTimeUtc).IsRequired();
        builder.Property(x => x.EndTimeUtc).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // Hot path: list-windows-for-interviewer ordered by day + start.
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.InterviewerAuthSubject,
            x.DayOfWeek,
        }).HasDatabaseName("ix_interviewer_availability_tenant_subject_day");
    }
}
