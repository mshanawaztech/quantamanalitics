using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Infrastructure.Esign;

/// <summary>
/// Deterministic stub for Phase 3. Mints a DocuSeal-shaped submission id and
/// signing URL derived from the EsignDocument row's id so retries are
/// idempotent. Phase 5 will swap this for an HTTP-backed implementation
/// against the real DocuSeal API.
/// </summary>
public sealed class StubDocuSealClient : IDocuSealClient
{
    private const string ScaffoldHost = "esign.scaffold.quantamanalitics.com";

    public Task<DocuSealSubmissionResult> CreateSubmissionAsync(
        EsignDocument pendingDocument,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pendingDocument);

        // DocuSeal submission ids are short integers in the real API; we use
        // a 16-char prefix of the row id so the stub is greppable.
        var suffix = pendingDocument.Id.ToString("N")[..16];
        var providerSubmissionId = $"sub_{suffix}";
        var signingUrl = $"https://{ScaffoldHost}/sign/{suffix}";

        return Task.FromResult(new DocuSealSubmissionResult(
            providerSubmissionId,
            signingUrl));
    }
}
