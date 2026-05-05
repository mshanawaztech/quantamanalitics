namespace QuantamAnalytics.Infrastructure.Tenancy;

/// <summary>
/// Request-scoped view of the current tenant derived from the authenticated
/// user. Null means either the request is anonymous or the identity has no
/// tenant assignment yet.
/// </summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }
}

/// <summary>
/// Internal write-side contract used by middleware to populate the tenant for
/// the current request before downstream services (like EF Core) run.
/// </summary>
public interface ICurrentTenantSetter
{
    void SetTenantId(Guid? tenantId);
}
