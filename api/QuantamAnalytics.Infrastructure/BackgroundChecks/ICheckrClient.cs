using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.BackgroundChecks;

/// <summary>
/// Boundary for the Checkr API. Phase 3 ships a deterministic stub so the
/// recruiter UX can be exercised end-to-end before any tenant has paid for
/// real Checkr credentials. Phase 5 swaps in an HTTP-backed implementation
/// without callers changing.
/// </summary>
public interface ICheckrClient
{
    /// <summary>
    /// Submit a report request to Checkr (or, for now, return a deterministic
    /// stub report id) and return the response we should persist on the
    /// <see cref="BackgroundCheck"/> aggregate.
    /// </summary>
    Task<CheckrReportRequestResult> RequestReportAsync(
        BackgroundCheck pendingCheck,
        CancellationToken cancellationToken);
}

public sealed record CheckrReportRequestResult(
    string ProviderReportId,
    BackgroundCheckStatus Status,
    string? StatusDetail);
