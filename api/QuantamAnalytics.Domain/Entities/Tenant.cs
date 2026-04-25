using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// A staffing firm using the platform. Top of the multi-tenant hierarchy —
/// every other tenant-scoped entity carries a TenantId pointing here.
/// </summary>
/// <remarks>
/// Slug is the URL-safe identifier exposed in subdomains and links
/// (e.g. <c>acme</c> → <c>acme.quantamanalitics.com</c>) and is the public
/// handle used during sign-in. It is unique and immutable once set.
/// </remarks>
public sealed class Tenant : IEntity
{
    // EF Core needs a parameterless ctor for materialization. Keep it private
    // so callers must go through the public ctor with required fields.
    private Tenant() { }

    public Tenant(string slug, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = Guid.CreateVersion7();
        Slug = slug.Trim().ToLowerInvariant();
        Name = name.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Lowercase, URL-safe identifier. Unique. Immutable.</summary>
    public string Slug { get; private set; } = default!;

    /// <summary>Display name shown in the UI. Free-form.</summary>
    public string Name { get; private set; } = default!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Soft-disable flag. Logins blocked when false (PR-06).</summary>
    public bool IsActive { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
