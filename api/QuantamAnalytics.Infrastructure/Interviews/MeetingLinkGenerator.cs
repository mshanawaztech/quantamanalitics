using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Interviews;

/// <summary>
/// Default <see cref="IMeetingLinkGenerator"/> for Phase 3. Produces a stable,
/// deterministic placeholder URL shaped like a real Google Meet or Microsoft
/// Teams join link but resolved entirely client-side. Phase 5 swaps this for
/// provider-API-backed generators.
/// </summary>
/// <remarks>
/// Determinism is intentional: the same interview event always resolves to
/// the same URL so re-renders, retries, and out-of-order webhook handlers
/// don't produce conflicting links.
/// </remarks>
public sealed class MeetingLinkGenerator : IMeetingLinkGenerator
{
    private const string ScaffoldHost = "meet.scaffold.quantamanalitics.com";

    public string Generate(InterviewCalendarProvider provider, Guid interviewEventId)
    {
        if (interviewEventId == Guid.Empty)
        {
            throw new ArgumentException("Interview event id is required.", nameof(interviewEventId));
        }

        // Provider-specific path segment so a future split into ZoomGenerator
        // / TeamsGenerator stays a drop-in replacement at the same URL shape.
        var providerSegment = provider switch
        {
            InterviewCalendarProvider.GoogleCalendar => "google",
            InterviewCalendarProvider.OutlookCalendar => "teams",
            _ => "scaffold",
        };

        // Compact, lowercase event id keeps the URL friendly when it's pasted
        // into a calendar invite description.
        return $"https://{ScaffoldHost}/{providerSegment}/{interviewEventId:N}";
    }
}
