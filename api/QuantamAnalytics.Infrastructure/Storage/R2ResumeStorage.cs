using Amazon.S3;
using Amazon.S3.Model;

namespace QuantamAnalytics.Infrastructure.Storage;

public sealed class R2ResumeStorage : IResumeStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;

    public R2ResumeStorage(IAmazonS3 s3, string bucketName)
    {
        _s3 = s3;
        _bucketName = bucketName;
    }

    public bool IsConfigured => true;

    public async Task<ResumeUploadResult> UploadAsync(
        Guid tenantId,
        string authSubject,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var safeFileName = Path.GetFileName(fileName).Replace(' ', '-').ToLowerInvariant();
        var objectKey = $"candidate-resumes/{tenantId}/{Uri.EscapeDataString(authSubject)}/{Guid.CreateVersion7()}-{safeFileName}";
        var uploadedAtUtc = DateTimeOffset.UtcNow;

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _s3.PutObjectAsync(request, cancellationToken);
        return new ResumeUploadResult(objectKey, uploadedAtUtc);
    }

    public Task<SignedDownloadUrl?> CreateSignedDownloadUrlAsync(
        string objectKey,
        TimeSpan ttl,
        string? downloadFileName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        if (ttl <= TimeSpan.Zero || ttl > TimeSpan.FromHours(1))
        {
            // Cap ttl at 1h — Phase 8 trust-center claim is "links expire fast".
            // The R2 SDK accepts longer pre-signed URLs but we don't.
            throw new ArgumentOutOfRangeException(nameof(ttl),
                "Signed download TTL must be between 1 second and 1 hour.");
        }

        var expiresAtUtc = DateTimeOffset.UtcNow.Add(ttl);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = expiresAtUtc.UtcDateTime,
        };

        if (!string.IsNullOrWhiteSpace(downloadFileName))
        {
            // Forces the browser to surface a sensible filename instead of
            // the random object key. ASCII-only to stay safe across S3
            // signers; richer encoding is a follow-up if needed.
            var safe = downloadFileName.Replace("\"", string.Empty);
            request.ResponseHeaderOverrides.ContentDisposition =
                $"attachment; filename=\"{safe}\"";
        }

        var url = _s3.GetPreSignedURL(request);
        return Task.FromResult<SignedDownloadUrl?>(new SignedDownloadUrl(url, expiresAtUtc));
    }
}
