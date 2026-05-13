using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Recruiter-applied label on an application. Tags power fast search,
/// saved filters, and bulk triage actions without leaking across tenants.
/// </summary>
public sealed class ApplicationTag : ITenantScoped
{
    private ApplicationTag() { }

    public ApplicationTag(
        Guid tenantId,
        Guid applicationId,
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        Name = Normalize(name);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Name { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return string.Join(
            ' ',
            value.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();
    }
}
