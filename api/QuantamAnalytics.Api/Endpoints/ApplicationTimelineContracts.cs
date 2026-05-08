namespace QuantamAnalytics.Api.Endpoints;

public sealed record ApplicationTimelineResponse(
    Guid ApplicationId,
    Guid CandidateProfileId,
    string CandidateName,
    string CandidateEmail,
    string JobTitle,
    string Status,
    TimelineEventResponse[] Events);

public sealed record TimelineEventResponse(
    Guid Id,
    string EventType,
    string Audience,
    string Title,
    string? Description,
    string ActorLabel,
    DateTimeOffset OccurredAtUtc);

public sealed record CandidateTimelineFeedItemResponse(
    Guid Id,
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    string JobSlug,
    string EventType,
    string Title,
    string? Detail,
    string ActorLabel,
    string Status,
    DateTimeOffset OccurredAtUtc);

public sealed record CandidateTimelineFeedResponse(
    CandidateTimelineFeedItemResponse[] Items);

public sealed record RecruiterCandidateActivityItemResponse(
    Guid Id,
    string CandidateName,
    string CandidateEmail,
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    string JobSlug,
    string EventType,
    string Title,
    string? Detail,
    string ActorLabel,
    string Status,
    DateTimeOffset OccurredAtUtc);

public sealed record RecruiterCandidateActivityResponse(
    RecruiterCandidateActivityItemResponse[] Items);

public sealed record AddApplicationTimelineCommentRequest(
    string Comment,
    bool VisibleToCandidate);
