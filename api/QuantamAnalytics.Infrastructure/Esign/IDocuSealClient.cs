using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Esign;

/// <summary>
/// Boundary for DocuSeal. Phase 3 ships a deterministic stub so the
/// recruiter UX (offer letter / onboarding packet handoff) can be exercised
/// end-to-end without a paid DocuSeal account. Phase 5 swaps in an HTTP-
/// backed implementation behind the same interface.
/// </summary>
public interface IDocuSealClient
{
    Task<DocuSealSubmissionResult> CreateSubmissionAsync(
        EsignDocument pendingDocument,
        CancellationToken cancellationToken);
}

public sealed record DocuSealSubmissionResult(
    string ProviderSubmissionId,
    string SigningUrl);
