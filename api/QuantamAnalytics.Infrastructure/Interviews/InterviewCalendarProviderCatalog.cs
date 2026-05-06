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
            "Queued",
            "Zoom and Teams join-link generation stays in the follow-up PR once provider records are live.")
    ];

    public IReadOnlyList<InterviewCalendarProviderSummary> GetProviders() => Providers;
}
