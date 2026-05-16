using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Maps an authenticated subject (Auth0 sub claim) to the tenant they belong
/// to. Read by the tenant-resolution middleware as a fallback when the JWT
/// does not carry a <c>tenant_id</c> custom claim — which happens when a
/// user signs up before the Auth0 post-login Action that mints the claim
/// has been configured for them.
/// </summary>
/// <remarks>
/// This table is intentionally NOT <c>ITenantScoped</c>: it tells you which
/// tenant a user belongs to, so it cannot itself be filtered by a tenant
/// the caller hasn't been assigned yet. The reflection-based query-filter
/// dispatcher in <c>AppDbContext</c> skips it for that reason.
///
/// One row per auth subject. A future "user belongs to multiple tenants"
/// model would need a different shape (and a UI for the user to choose
/// the active tenant at sign-in). Out of scope here.
/// </remarks>
public sealed class TenantMembership : IEntity
{
    private TenantMembership() { }

    public TenantMembership(string authSubject, Guid tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authSubject);

        Id = Guid.CreateVersion7();
        AuthSubject = authSubject.Trim();
        TenantId = tenantId;
        JoinedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    /// <summary>Auth0 <c>sub</c> claim, e.g. <c>auth0|abc123</c>.</summary>
    public string AuthSubject { get; private set; } = default!;

    public Guid TenantId { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }
}
