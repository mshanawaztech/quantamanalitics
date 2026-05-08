using System.Security.Claims;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Tenancy;

/// <summary>
/// Resolves the current tenant + auth subject from the authenticated
/// principal's claims and stores them in scoped accessors for downstream
/// consumers (EF Core query filter, audit-log interceptor).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(
        HttpContext context,
        ICurrentTenantSetter currentTenant,
        ICurrentUserSetter currentUser)
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

        // Auth0 sub claim — usually "auth0|abc123". The audit-log
        // interceptor records this; tenant scope alone isn't enough for
        // SOC 2 evidence (auditors need to know who, not just where).
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        currentUser.SetAuthSubject(subject);

        return _next(context);
    }
}
