using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Represents a candidate's application to a job opening. Tracks the pipeline
/// state progression from Applied through Hired or Rejected. Owned by a tenant.
/// </summary>
public sealed class Application : ITenantScoped
{
    private Application() { }

    public Application(
        Guid tenantId,
        Guid jobId,
        Guid candidateProfileId,
        string candidateEmail,
        string candidateName,
        string? note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateName);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        JobId = jobId;
        CandidateProfileId = candidateProfileId;
        CandidateEmail = candidateEmail.Trim().ToLowerInvariant();
        CandidateName = candidateName.Trim();
        Note = Normalize(note);
        Status = ApplicationStatus.Applied;
        AppliedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = AppliedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid JobId { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public string CandidateEmail { get; private set; } = default!;
    public string CandidateName { get; private set; } = default!;
    public string? Note { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public DateTimeOffset AppliedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void TransitionToInterviewing() => TransitionTo(ApplicationStatus.Interviewing);
    public void TransitionToOfferSent() => TransitionTo(ApplicationStatus.OfferSent);
    public void TransitionToHired() => TransitionTo(ApplicationStatus.Hired);
    public void TransitionToRejected() => TransitionTo(ApplicationStatus.Rejected);

    private void TransitionTo(ApplicationStatus newStatus)
    {
        if (!IsValidTransition(Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {newStatus}.");
        }

        Status = newStatus;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidTransition(ApplicationStatus from, ApplicationStatus to)
    {
        // Terminal states cannot be left
        if (from == ApplicationStatus.Hired || from == ApplicationStatus.Rejected)
        {
            return false;
        }

        // Non-terminal can move forward or to rejected
        return true;
    }
}

public enum ApplicationStatus
{
    Applied = 0,
    Interviewing = 1,
    OfferSent = 2,
    Hired = 3,
    Rejected = 4,
}
