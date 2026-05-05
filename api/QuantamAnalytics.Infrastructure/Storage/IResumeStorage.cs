namespace QuantamAnalytics.Infrastructure.Storage;

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
}

public sealed record ResumeUploadResult(string ObjectKey, DateTimeOffset UploadedAtUtc);
