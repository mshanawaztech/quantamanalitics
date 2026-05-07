namespace QuantamAnalytics.Infrastructure.Interviews;

public sealed class InterviewCalendarProviderCatalog : IInterviewCalendarProviderCatalog
{
    private static readonly InterviewCalendarProviderSummary[] Providers =
    [
        new(
            "Google Calendar baseline",
            "Baseline ready",
            "Persistent interview events can now be linked to Google calendar records in the next slice."),
        new(
            "Outlook baseline",
            "Baseline ready",
            "The same interview event model is available for Microsoft-centered teams without branching the workflow."),
        new(
            "Meeting link scaffold",
            "Generator wired",
            "Recruiters can mint a deterministic Google Meet / Teams-shaped join link per interview event; provider-API integration arrives in Phase 5.")
    ];

    public IReadOnlyList<InterviewCalendarProviderSummary> GetProviders() => Providers;
}
