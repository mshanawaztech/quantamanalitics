using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Saved recruiter search/filter preset for the applications pipeline.
/// Presets are tenant-scoped and private to the recruiter who created them.
/// </summary>
public sealed class RecruiterApplicationFilterPreset : ITenantScoped
{
    private RecruiterApplicationFilterPreset() { }

    public RecruiterApplicationFilterPreset(
        Guid tenantId,
        string createdByAuthSubject,
        string name,
        string? search,
        string? status,
        string? tag,
        string? location,
        bool stuckOnly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        CreatedByAuthSubject = createdByAuthSubject.Trim();
        Name = name.Trim();
        Search = Normalize(search);
        Status = Normalize(status);
        Tag = Normalize(tag);
        Location = Normalize(location);
        StuckOnly = stuckOnly;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string CreatedByAuthSubject { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Search { get; private set; }
    public string? Status { get; private set; }
    public string? Tag { get; private set; }
    public string? Location { get; private set; }
    public bool StuckOnly { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void UpdateCriteria(
        string name,
        string? search,
        string? status,
        string? tag,
        string? location,
        bool stuckOnly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Search = Normalize(search);
        Status = Normalize(status);
        Tag = Normalize(tag);
        Location = Normalize(location);
        StuckOnly = stuckOnly;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
