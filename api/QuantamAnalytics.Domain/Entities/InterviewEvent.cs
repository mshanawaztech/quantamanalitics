using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Persistent interview event scheduled off a recruiter/client submission.
/// This Phase 3 baseline gives calendar-provider work a stable tenant-scoped
/// aggregate before meeting links and deeper sync behavior land.
/// </summary>
public sealed class InterviewEvent : ITenantScoped
{
    private InterviewEvent() { }

    public InterviewEvent(
        Guid tenantId,
        Guid submissionId,
        string candidateName,
        string candidateEmail,
        string title,
        string interviewerName,
        InterviewCalendarProvider provider,
        DateTimeOffset scheduledStartUtc,
        DateTimeOffset scheduledEndUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(interviewerName);

        if (scheduledEndUtc <= scheduledStartUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduledEndUtc), "Interview end time must be after the start time.");
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        SubmissionId = submissionId;
        CandidateName = candidateName.Trim();
        CandidateEmail = candidateEmail.Trim().ToLowerInvariant();
        Title = title.Trim();
        InterviewerName = interviewerName.Trim();
        Provider = provider;
        Status = InterviewEventStatus.Scheduled;
        ScheduledStartUtc = scheduledStartUtc;
        ScheduledEndUtc = scheduledEndUtc;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public string CandidateName { get; private set; } = default!;
    public string CandidateEmail { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string InterviewerName { get; private set; } = default!;
    public InterviewCalendarProvider Provider { get; private set; }
    public InterviewEventStatus Status { get; private set; }
    public DateTimeOffset ScheduledStartUtc { get; private set; }
    public DateTimeOffset ScheduledEndUtc { get; private set; }
    public string? ExternalEventId { get; private set; }
    public string? MeetingJoinUrl { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void AttachProviderReference(string externalEventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalEventId);

        ExternalEventId = externalEventId.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void AttachMeetingJoinUrl(string meetingJoinUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meetingJoinUrl);

        MeetingJoinUrl = meetingJoinUrl.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (Status == InterviewEventStatus.Cancelled)
        {
            return;
        }

        Status = InterviewEventStatus.Cancelled;
        CancelledAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CancelledAtUtc.Value;
    }
}

public enum InterviewCalendarProvider
{
    GoogleCalendar = 0,
    OutlookCalendar = 1,
}

public enum InterviewEventStatus
{
    Scheduled = 0,
    Cancelled = 1,
}
