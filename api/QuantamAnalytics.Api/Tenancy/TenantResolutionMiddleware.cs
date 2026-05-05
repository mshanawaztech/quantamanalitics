using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Tenancy;

/// <summary>
/// Resolves the current tenant from the authenticated principal's custom JWT
/// claim and stores it in a scoped accessor for downstream consumers.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context, ICurrentTenantSetter currentTenant)
    {
        var tenantClaim = context.User.FindFirst(Roles.TenantIdClaim)?.Value;

        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            currentTenant.SetTenantId(tenantId);
        }
        else
        {
            currentTenant.SetTenantId(null);
        }

        return _next(context);
    }
}
