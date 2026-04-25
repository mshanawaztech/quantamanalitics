namespace QuantamAnalytics.Domain.Common;

/// <summary>
/// Marker for any persisted aggregate root. All entities use a UUID v7 primary
/// key (time-ordered, distributed-friendly, no DB roundtrip to allocate).
/// </summary>
public interface IEntity
{
    Guid Id { get; }
}

/// <summary>
/// Marker for tenant-scoped data. EF Core's global query filter (added in PR-07)
/// uses this interface to inject `WHERE tenant_id = @currentTenant` into every
/// query — preventing cross-tenant data leaks at the data layer.
/// </summary>
public interface ITenantScoped : IEntity
{
    Guid TenantId { get; }
}
