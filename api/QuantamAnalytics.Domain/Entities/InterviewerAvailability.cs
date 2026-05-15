using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Phase 9 / Story 68 — a recurring availability window an interviewer
/// publishes so candidates can self-book without back-and-forth.
///
/// Windows are stored in UTC; the recruiter dashboard renders them in
/// the viewer's timezone. Day-of-week is 0–6 (Sunday=0) per the .NET
/// DayOfWeek enum. Times are stored as TimeOnly so DST jumps don't
/// shift a 9am window to 8am twice a year.
/// </summary>
public sealed class InterviewerAvailability : ITenantScoped
{
    private InterviewerAvailability() { }

    public InterviewerAvailability(
        Guid tenantId,
        string interviewerAuthSubject,
        DayOfWeek dayOfWeek,
        TimeOnly startTimeUtc,
        TimeOnly endTimeUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interviewerAuthSubject);
        if (endTimeUtc <= startTimeUtc)
        {
            throw new ArgumentException(
                "Availability end must be strictly after start.",
                nameof(endTimeUtc));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        InterviewerAuthSubject = interviewerAuthSubject.Trim();
        DayOfWeek = dayOfWeek;
        StartTimeUtc = startTimeUtc;
        EndTimeUtc = endTimeUtc;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string InterviewerAuthSubject { get; private set; } = default!;
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTimeUtc { get; private set; }
    public TimeOnly EndTimeUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
