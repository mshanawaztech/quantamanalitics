namespace QuantamAnalytics.Infrastructure.Storage;

/// <summary>
/// Tenant-scoped storage for candidate resumes plus other private documents
/// served from R2. Implementations must enforce that a download URL belongs
/// to the caller's tenant — the recipient check happens at the endpoint
/// layer because object keys alone cannot be trusted from the client.
/// </summary>
public interface IResumeStorage
{
    bool IsConfigured { get; }

    Task<ResumeUploadResult> UploadAsync(
        Guid tenantId,
        string authSubject,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);

    /// <summary>
    /// Mint a short-lived signed URL for downloading an object the caller
    /// already proved (at the endpoint layer) they own. The URL expires
    /// after <paramref name="ttl"/>; clients re-request before each
    /// download. Returns null when storage is disabled.
    /// </summary>
    Task<SignedDownloadUrl?> CreateSignedDownloadUrlAsync(
        string objectKey,
        TimeSpan ttl,
        string? downloadFileName,
        CancellationToken cancellationToken);
}

public sealed record ResumeUploadResult(string ObjectKey, DateTimeOffset UploadedAtUtc);

public sealed record SignedDownloadUrl(string Url, DateTimeOffset ExpiresAtUtc);
