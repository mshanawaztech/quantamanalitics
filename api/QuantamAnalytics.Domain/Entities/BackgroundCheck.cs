using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Tenant-scoped record of a Checkr background check tied to a submitted
/// candidate. Phase 3 ships the request shape + status state machine + the
/// webhook-ready persistence surface; the live Checkr API integration arrives
/// in Phase 5 once recruiter accounts have a real credential to sign requests
/// with.
/// </summary>
public sealed class BackgroundCheck : ITenantScoped
{
    private BackgroundCheck() { }

    public BackgroundCheck(
        Guid tenantId,
        Guid candidateProfileId,
        string candidateEmail,
        string candidateName,
        string requestedByAuthSubject,
        string packageSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedByAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageSlug);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        CandidateProfileId = candidateProfileId;
        CandidateEmail = candidateEmail.Trim().ToLowerInvariant();
        CandidateName = candidateName.Trim();
        RequestedByAuthSubject = requestedByAuthSubject.Trim();
        PackageSlug = packageSlug.Trim().ToLowerInvariant();
        Status = BackgroundCheckStatus.Requested;
        RequestedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = RequestedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public string CandidateEmail { get; private set; } = default!;
    public string CandidateName { get; private set; } = default!;
    public string RequestedByAuthSubject { get; private set; } = default!;

    /// <summary>Checkr "package" identifier, e.g. <c>tasker_standard</c>.</summary>
    public string PackageSlug { get; private set; } = default!;

    /// <summary>Provider-side report id once Checkr returns one. Null until the request is acknowledged.</summary>
    public string? ProviderReportId { get; private set; }

    public BackgroundCheckStatus Status { get; private set; }
    public string? StatusDetail { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Called from <see cref="ICheckrClient.RequestReportAsync"/> after the
    /// stub (or eventual real Checkr call) returns a report id. Idempotent:
    /// re-attaching the same provider id is a no-op.
    /// </summary>
    public void AttachProviderReport(string providerReportId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerReportId);

        if (ProviderReportId == providerReportId)
        {
            return;
        }

        ProviderReportId = providerReportId.Trim();
        Status = BackgroundCheckStatus.InProgress;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Called from the webhook handler when Checkr publishes a final status.
    /// Supports the three Checkr terminal outcomes plus an explicit cancel
    /// path that recruiters can trigger from the UI.
    /// </summary>
    public void RecordTerminalStatus(BackgroundCheckStatus status, string? detail)
    {
        if (status is not BackgroundCheckStatus.Clear
                  and not BackgroundCheckStatus.Consider
                  and not BackgroundCheckStatus.Cancelled)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                "Only Clear, Consider, or Cancelled are valid terminal statuses.");
        }

        if (Status is BackgroundCheckStatus.Clear or BackgroundCheckStatus.Consider or BackgroundCheckStatus.Cancelled)
        {
            // Already terminal — webhooks can fire late or out of order.
            // Refuse to silently rewrite history.
            return;
        }

        Status = status;
        StatusDetail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        CompletedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CompletedAtUtc.Value;
    }
}

/// <summary>
/// Mirrors Checkr's published states. <c>Requested</c> is our pre-Checkr
/// state (the row exists in our DB but no provider id yet). The rest map
/// 1:1 onto Checkr report statuses.
/// </summary>
public enum BackgroundCheckStatus
{
    Requested = 0,
    InProgress = 1,
    Clear = 2,
    Consider = 3,
    Cancelled = 4,
}
