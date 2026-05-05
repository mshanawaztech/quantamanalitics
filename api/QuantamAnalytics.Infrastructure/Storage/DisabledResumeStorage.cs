namespace QuantamAnalytics.Infrastructure.Storage;

public sealed class DisabledResumeStorage : IResumeStorage
{
    public bool IsConfigured => false;

    public Task<ResumeUploadResult> UploadAsync(
        Guid tenantId,
        string authSubject,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(
            "Cloudflare R2 resume storage is not configured for this environment.");
    }
}
