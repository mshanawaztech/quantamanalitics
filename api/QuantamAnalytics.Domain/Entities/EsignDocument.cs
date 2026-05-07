using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Tenant-scoped record of a document Quantam Analytics has handed off to
/// DocuSeal for the candidate to sign — the offer letter the recruiter
/// extends, or the onboarding packet bundled before placement.
/// </summary>
/// <remarks>
/// Phase 3 ships the request shape, status state machine, and webhook
/// landing pad. Live DocuSeal API integration arrives in Phase 5.
/// </remarks>
public sealed class EsignDocument : ITenantScoped
{
    private EsignDocument() { }

    public EsignDocument(
        Guid tenantId,
        Guid candidateProfileId,
        string candidateEmail,
        string candidateName,
        string sentByAuthSubject,
        EsignDocumentKind kind,
        string templateSlug,
        string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sentByAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        CandidateProfileId = candidateProfileId;
        CandidateEmail = candidateEmail.Trim().ToLowerInvariant();
        CandidateName = candidateName.Trim();
        SentByAuthSubject = sentByAuthSubject.Trim();
        Kind = kind;
        TemplateSlug = templateSlug.Trim().ToLowerInvariant();
        Subject = subject.Trim();
        Status = EsignDocumentStatus.Drafted;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public string CandidateEmail { get; private set; } = default!;
    public string CandidateName { get; private set; } = default!;
    public string SentByAuthSubject { get; private set; } = default!;

    public EsignDocumentKind Kind { get; private set; }

    /// <summary>DocuSeal template id, e.g. <c>offer_letter_v1</c>.</summary>
    public string TemplateSlug { get; private set; } = default!;

    /// <summary>Subject line presented to the signer in the DocuSeal email.</summary>
    public string Subject { get; private set; } = default!;

    /// <summary>DocuSeal "submission" id once the provider returns one.</summary>
    public string? ProviderSubmissionId { get; private set; }

    /// <summary>Signing URL returned by DocuSeal — recruiters surface it in the SPA.</summary>
    public string? SigningUrl { get; private set; }

    public EsignDocumentStatus Status { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public DateTimeOffset? ViewedAtUtc { get; private set; }
    public DateTimeOffset? SignedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Called from <see cref="IDocuSealClient.CreateSubmissionAsync"/> after
    /// the stub (or eventual real DocuSeal call) returns. Idempotent: re-
    /// attaching the same provider id is a no-op.
    /// </summary>
    public void AttachProviderSubmission(string providerSubmissionId, string signingUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSubmissionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signingUrl);

        if (ProviderSubmissionId == providerSubmissionId)
        {
            return;
        }

        ProviderSubmissionId = providerSubmissionId.Trim();
        SigningUrl = signingUrl.Trim();
        Status = EsignDocumentStatus.Sent;
        SentAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = SentAtUtc.Value;
    }

    public void MarkViewed()
    {
        if (Status is EsignDocumentStatus.Signed or EsignDocumentStatus.Cancelled)
        {
            // Late webhooks must not regress a terminal status.
            return;
        }

        Status = EsignDocumentStatus.Viewed;
        ViewedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = ViewedAtUtc.Value;
    }

    public void MarkSigned()
    {
        if (Status is EsignDocumentStatus.Cancelled)
        {
            return;
        }

        Status = EsignDocumentStatus.Signed;
        SignedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = SignedAtUtc.Value;
    }

    public void Cancel()
    {
        if (Status is EsignDocumentStatus.Signed)
        {
            // Already complete — can't cancel a signed document.
            throw new InvalidOperationException("Signed e-sign documents cannot be cancelled.");
        }

        Status = EsignDocumentStatus.Cancelled;
        CancelledAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CancelledAtUtc.Value;
    }
}

public enum EsignDocumentKind
{
    OfferLetter = 0,
    OnboardingPacket = 1,
}

public enum EsignDocumentStatus
{
    Drafted = 0,
    Sent = 1,
    Viewed = 2,
    // CA1720 flags `Signed` because it matches the C/C++ `signed` keyword (not
    // even a C# type alias). The e-sign domain term is the right name here —
    // renaming to `FullySigned` or `Completed` would be worse for readers.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "Signed is the canonical e-signature domain term.")]
    Signed = 3,
    Cancelled = 4,
}
