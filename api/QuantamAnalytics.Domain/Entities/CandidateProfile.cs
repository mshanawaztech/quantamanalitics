using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Candidate-facing profile owned by a tenant and linked to one authenticated
/// Auth0 subject. Stores profile fields plus resume metadata used by later
/// application flows.
/// </summary>
public sealed class CandidateProfile : ITenantScoped
{
    private CandidateProfile() { }

    public CandidateProfile(
        Guid tenantId,
        string authSubject,
        string email,
        string? fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        AuthSubject = authSubject.Trim();
        Email = email.Trim().ToLowerInvariant();
        FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string AuthSubject { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string? FullName { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Headline { get; private set; }
    public string? Summary { get; private set; }
    public string? ResumeObjectKey { get; private set; }
    public string? ResumeFileName { get; private set; }
    public DateTimeOffset? ResumeUploadedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void UpdateProfile(
        string email,
        string? fullName,
        string? phoneNumber,
        string? headline,
        string? summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Email = email.Trim().ToLowerInvariant();
        FullName = Normalize(fullName);
        PhoneNumber = Normalize(phoneNumber);
        Headline = Normalize(headline);
        Summary = Normalize(summary);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void AttachResume(
        string objectKey,
        string fileName,
        DateTimeOffset uploadedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        ResumeObjectKey = objectKey.Trim();
        ResumeFileName = fileName.Trim();
        ResumeUploadedAtUtc = uploadedAtUtc;
        UpdatedAtUtc = uploadedAtUtc;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
