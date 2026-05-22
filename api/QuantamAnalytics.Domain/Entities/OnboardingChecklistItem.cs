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
/// Onboarding artifact types, grouped into phases by <see cref="OnboardingPhases"/>.
/// Persisted as a string (max 32 chars) — keep new names short and NEVER
/// rename an existing value (it would orphan stored rows). <see cref="Custom"/>
/// is the escape hatch for one-off recruiter-defined items.
/// </summary>
public enum OnboardingItemType
{
    // ── Phase 1: Identity & eligibility ──
    W4 = 0,
    I9 = 1,
    StateTaxForm = 4,
    GovernmentId = 6,
    WorkAuthorization = 7,
    TaxFormW9 = 8,

    // ── Phase 2: Professional background ──
    Resume = 10,
    Certification = 11,
    ProfessionalReference = 12,
    EducationVerification = 13,

    // ── Phase 3: Agreements ──
    HandbookAcknowledgement = 5,
    ServicesAgreement = 20,
    Nda = 21,
    OfferLetter = 22,
    ClientCompliance = 23,

    // ── Phase 4: Screening ──
    BackgroundCheck = 30,
    DrugScreen = 31,

    // ── Phase 5: Payroll & logistics ──
    DirectDeposit = 2,
    EmergencyContact = 3,
    EquipmentAccess = 40,

    Custom = 99,
}

/// <summary>The five real-world onboarding phases a checklist groups into.</summary>
public enum OnboardingPhase
{
    IdentityEligibility = 0,
    ProfessionalBackground = 1,
    Agreements = 2,
    Screening = 3,
    PayrollLogistics = 4,
    General = 99,
}

/// <summary>
/// Worker classification — drives which default onboarding template applies.
/// Contractors file a W-9 + sign an SOW; employees file a W-4/I-9 + get an
/// offer letter and handbook; vendors are company-level with an MSA.
/// </summary>
public enum ConsultantType
{
    Contractor = 0,
    FullTime = 1,
    Vendor = 2,
}

/// <summary>
/// Maps each onboarding item type to its phase and whether it's typically
/// required. Pure metadata (no storage) so the API can group and order a
/// consultant's checklist into a guided, phased experience.
/// </summary>
public static class OnboardingPhases
{
    public static OnboardingPhase PhaseFor(OnboardingItemType type) => type switch
    {
        OnboardingItemType.W4 or OnboardingItemType.I9 or OnboardingItemType.StateTaxForm
            or OnboardingItemType.GovernmentId or OnboardingItemType.WorkAuthorization
            or OnboardingItemType.TaxFormW9 => OnboardingPhase.IdentityEligibility,
        OnboardingItemType.Resume or OnboardingItemType.Certification
            or OnboardingItemType.ProfessionalReference
            or OnboardingItemType.EducationVerification => OnboardingPhase.ProfessionalBackground,
        OnboardingItemType.HandbookAcknowledgement or OnboardingItemType.ServicesAgreement
            or OnboardingItemType.Nda or OnboardingItemType.OfferLetter
            or OnboardingItemType.ClientCompliance => OnboardingPhase.Agreements,
        OnboardingItemType.BackgroundCheck or OnboardingItemType.DrugScreen => OnboardingPhase.Screening,
        OnboardingItemType.DirectDeposit or OnboardingItemType.EmergencyContact
            or OnboardingItemType.EquipmentAccess => OnboardingPhase.PayrollLogistics,
        _ => OnboardingPhase.General,
    };

    /// <summary>Optional-by-default item types; everything else is required.</summary>
    public static bool IsRequired(OnboardingItemType type) => type switch
    {
        OnboardingItemType.Certification or OnboardingItemType.EducationVerification
            or OnboardingItemType.ClientCompliance or OnboardingItemType.DrugScreen
            or OnboardingItemType.Custom => false,
        _ => true,
    };

    /// <summary>Stable display order: phase first, then a fixed within-phase rank.</summary>
    public static int SortOrder(OnboardingItemType type) =>
        (int)PhaseFor(type) * 100 + (int)type;
}

public enum OnboardingItemStatus
{
    Pending = 0,
    Submitted = 1,
    Approved = 2,
}
