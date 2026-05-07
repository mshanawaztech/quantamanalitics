using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Interviews;

/// <summary>
/// Generates a join URL for an interview event so the recruiter, candidate,
/// and interviewer can all click into the same call.
/// </summary>
/// <remarks>
/// Phase 3 ships a deterministic-stub implementation: the URL is shaped like
/// a real Zoom or Teams join link but does not call any provider API. It
/// exists so the persistence + UI flow (`InterviewEvent.AttachMeetingJoinUrl`,
/// the recruiter overview surface) can be exercised end-to-end on the
/// existing free-tier infra.
///
/// Phase 5 will replace the stub with provider-specific implementations
/// (`ZoomMeetingLinkGenerator`, `TeamsMeetingLinkGenerator`) and pick the
/// concrete generator off <see cref="InterviewCalendarProvider"/>.
/// </remarks>
public interface IMeetingLinkGenerator
{
    /// <summary>
    /// Returns a join URL for the given interview event. Idempotent — the
    /// same provider + event id always produces the same URL.
    /// </summary>
    string Generate(InterviewCalendarProvider provider, Guid interviewEventId);
}
