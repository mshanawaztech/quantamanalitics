namespace QuantamAnalytics.Infrastructure.Interviews;

public interface IInterviewCalendarProviderCatalog
{
    IReadOnlyList<InterviewCalendarProviderSummary> GetProviders();
}

public sealed record InterviewCalendarProviderSummary(
    string Name,
    string Status,
    string Detail);
