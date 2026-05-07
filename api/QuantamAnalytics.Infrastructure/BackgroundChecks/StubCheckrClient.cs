using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.BackgroundChecks;

/// <summary>
/// Deterministic stub for Phase 3. Mints a Checkr-shaped report id derived
/// from the BackgroundCheck row's id so retries are idempotent. Reports
/// always come back as <see cref="BackgroundCheckStatus.InProgress"/> here;
/// the webhook simulation endpoint is what flips the row terminal in dev.
/// </summary>
public sealed class StubCheckrClient : ICheckrClient
{
    public Task<CheckrReportRequestResult> RequestReportAsync(
        BackgroundCheck pendingCheck,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pendingCheck);

        // Checkr report ids look like "rep_<22-char-base32-ish>". We mirror
        // the prefix and derive the suffix from our row id so the stub is
        // greppable and reproducible.
        var suffix = pendingCheck.Id.ToString("N")[..22];
        var providerReportId = $"rep_{suffix}";

        return Task.FromResult(new CheckrReportRequestResult(
            providerReportId,
            BackgroundCheckStatus.InProgress,
            "Stub Checkr request — Phase 5 will replace this with a real HTTP call."));
    }
}
