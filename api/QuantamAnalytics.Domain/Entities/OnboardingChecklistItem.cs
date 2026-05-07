using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// One row of a candidate's onboarding checklist — a tax form, a direct
/// deposit setup, an emergency-contact card, or a custom item the recruiter
/// added. Each row tracks its own state independent of the others so the
/// candidate dashboard can render a per-item progress meter.
/// </summary>
/// <remarks>
/// Phase 3 ships the lifecycle (Pending → Submitted → Approved/Rejected)
/// and the recruiter / candidate read+write surfaces. The actual document
/// upload sits on top of the existing R2 storage path; here we only carry
/// metadata + status.
/// </remarks>
public sealed class OnboardingChecklistItem : ITenantScoped
{
    private OnboardingChecklistItem() { }

    public OnboardingChecklistItem(
        Guid tenantId,
        Guid candidateProfileId,
        string assignedByAuthSubject,
        OnboardingItemType itemType,
        string title,
        string? instructions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignedByAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        CandidateProfileId = candidateProfileId;
        AssignedByAuthSubject = assignedByAuthSubject.Trim();
        ItemType = itemType;
        Title = title.Trim();
        Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();
        Status = OnboardingItemStatus.Pending;
        AssignedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = AssignedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public string AssignedByAuthSubject { get; private set; } = default!;

    public OnboardingItemType ItemType { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Instructions { get; private set; }

    public OnboardingItemStatus Status { get; private set; }

    /// <summary>
    /// Free-form note from the candidate when they submit (e.g. "Sent via
    /// HR portal yesterday"). Optional.
    /// </summary>
    public string? CandidateNote { get; private set; }

    /// <summary>Recruiter feedback on rejection. Optional.</summary>
    public string? ReviewerNote { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Submit(string? candidateNote)
    {
        if (Status is OnboardingItemStatus.Approved)
        {
            throw new InvalidOperationException("Approved items cannot be re-submitted.");
        }

        Status = OnboardingItemStatus.Submitted;
        CandidateNote = string.IsNullOrWhiteSpace(candidateNote) ? null : candidateNote.Trim();
        SubmittedAtUtc = DateTimeOffset.UtcNow;
        ReviewerNote = null;
        ReviewedAtUtc = null;
        UpdatedAtUtc = SubmittedAtUtc.Value;
    }

    public void Approve(string? reviewerNote)
    {
        if (Status is not OnboardingItemStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted items can be approved.");
        }

        Status = OnboardingItemStatus.Approved;
        ReviewerNote = string.IsNullOrWhiteSpace(reviewerNote) ? null : reviewerNote.Trim();
        ReviewedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = ReviewedAtUtc.Value;
    }

    public void Reject(string reviewerNote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerNote);

        if (Status is not OnboardingItemStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted items can be rejected.");
        }

        // Rejection sends the row back to Pending so the candidate can re-submit.
        Status = OnboardingItemStatus.Pending;
        ReviewerNote = reviewerNote.Trim();
        ReviewedAtUtc = DateTimeOffset.UtcNow;
        SubmittedAtUtc = null;
        UpdatedAtUtc = ReviewedAtUtc.Value;
    }
}

/// <summary>
/// Common onboarding artifact types. <see cref="Custom"/> is the escape
/// hatch for one-off recruiter-defined items so we don't have to ship a
/// migration every time a tenant adds a new category.
/// </summary>
public enum OnboardingItemType
{
    W4 = 0,
    I9 = 1,
    DirectDeposit = 2,
    EmergencyContact = 3,
    StateTaxForm = 4,
    HandbookAcknowledgement = 5,
    Custom = 99,
}

public enum OnboardingItemStatus
{
    Pending = 0,
    Submitted = 1,
    Approved = 2,
}
