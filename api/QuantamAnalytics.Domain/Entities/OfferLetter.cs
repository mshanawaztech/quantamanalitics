using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Phase 9 / Story 69 — offer-letter aggregate. State machine:
///
///   Drafted → Sent → (Accepted | Declined | Withdrawn)
///
/// Generation today produces a markdown body the recruiter can tweak;
/// the real send flow hands off to DocuSeal (Phase 5 / PR-39 once the
/// real client lands) or stays inline as a recruiter-tracked artifact.
/// </summary>
public sealed class OfferLetter : ITenantScoped
{
    private OfferLetter() { }

    public OfferLetter(
        Guid tenantId,
        Guid candidateProfileId,
        Guid? applicationId,
        string title,
        decimal baseAnnualSalary,
        string currency,
        DateOnly startDate,
        string bodyMarkdown,
        string createdByAuthSubject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyMarkdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByAuthSubject);
        if (baseAnnualSalary < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseAnnualSalary), "Base salary must be non-negative.");
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        CandidateProfileId = candidateProfileId;
        ApplicationId = applicationId;
        Title = title.Trim();
        BaseAnnualSalary = baseAnnualSalary;
        Currency = currency.Trim().ToUpperInvariant();
        StartDate = startDate;
        BodyMarkdown = bodyMarkdown.Trim();
        CreatedByAuthSubject = createdByAuthSubject.Trim();
        Status = OfferLetterStatus.Drafted;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public string Title { get; private set; } = default!;
    public decimal BaseAnnualSalary { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateOnly StartDate { get; private set; }
    public string BodyMarkdown { get; private set; } = default!;
    public string CreatedByAuthSubject { get; private set; } = default!;
    public OfferLetterStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public string? CandidateResponseNote { get; private set; }

    public void UpdateDraft(string bodyMarkdown, decimal baseAnnualSalary, DateOnly startDate)
    {
        ExpectStatus(OfferLetterStatus.Drafted);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyMarkdown);
        if (baseAnnualSalary < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseAnnualSalary), "Base salary must be non-negative.");
        }
        BodyMarkdown = bodyMarkdown.Trim();
        BaseAnnualSalary = baseAnnualSalary;
        StartDate = startDate;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Send()
    {
        ExpectStatus(OfferLetterStatus.Drafted);
        Status = OfferLetterStatus.Sent;
        SentAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = SentAtUtc.Value;
    }

    public void RecordAcceptance(string? note)
    {
        ExpectStatus(OfferLetterStatus.Sent);
        Status = OfferLetterStatus.Accepted;
        CandidateResponseNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ResolvedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = ResolvedAtUtc.Value;
    }

    public void RecordDecline(string? note)
    {
        ExpectStatus(OfferLetterStatus.Sent);
        Status = OfferLetterStatus.Declined;
        CandidateResponseNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ResolvedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = ResolvedAtUtc.Value;
    }

    public void Withdraw()
    {
        if (Status is OfferLetterStatus.Accepted or OfferLetterStatus.Declined)
        {
            throw new InvalidOperationException(
                "Cannot withdraw an offer that has already been resolved.");
        }
        Status = OfferLetterStatus.Withdrawn;
        ResolvedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = ResolvedAtUtc.Value;
    }

    private void ExpectStatus(OfferLetterStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Offer is in status {Status}; expected {expected}.");
        }
    }
}

public enum OfferLetterStatus
{
    Drafted = 0,
    Sent = 1,
    Accepted = 2,
    Declined = 3,
    Withdrawn = 4,
}
