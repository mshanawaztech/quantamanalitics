using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Immutable activity event attached to a candidate application timeline.
/// Recruiters see all events; candidates only see events marked for both
/// audiences.
/// </summary>
public sealed class ApplicationTimelineEvent : ITenantScoped
{
    private ApplicationTimelineEvent() { }

    public ApplicationTimelineEvent(
        Guid tenantId,
        Guid applicationId,
        Guid candidateProfileId,
        ApplicationTimelineEventType eventType,
        ApplicationTimelineAudience audience,
        string title,
        string? description,
        string actorLabel,
        DateTimeOffset? occurredAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorLabel);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        CandidateProfileId = candidateProfileId;
        EventType = eventType;
        Audience = audience;
        Title = title.Trim();
        Description = Normalize(description);
        ActorLabel = actorLabel.Trim();
        OccurredAtUtc = occurredAtUtc ?? DateTimeOffset.UtcNow;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public ApplicationTimelineEventType EventType { get; private set; }
    public ApplicationTimelineAudience Audience { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public string ActorLabel { get; private set; } = default!;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum ApplicationTimelineEventType
{
    Applied = 0,
    RecruiterReviewed = 1,
    NoteAdded = 2,
    StageChanged = 3,
    InterviewScheduled = 4,
    OfferPrepared = 5,
}

public enum ApplicationTimelineAudience
{
    CandidateAndRecruiter = 0,
    RecruiterOnly = 1,
}
