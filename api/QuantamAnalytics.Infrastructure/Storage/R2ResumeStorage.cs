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
}
