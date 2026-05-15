using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Tenancy;

/// <summary>
/// Resolves the current tenant + auth subject from the authenticated
/// principal's claims and stores them in scoped accessors for downstream
/// consumers (EF Core query filter, audit-log interceptor).
///
/// When the JWT does not carry a <c>tenant_id</c> custom claim (the user
/// signed in before the Auth0 post-login Action that mints the claim ran
/// for their account), we fall back to a tenant-memberships lookup keyed
/// on the auth subject so they don't end up locked out of every portal.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenantSetter currentTenant,
        ICurrentUserSetter currentUser,
        AppDbContext db)
    {
        var tenantClaim = context.User.FindFirst(Roles.TenantIdClaim)?.Value;
        Guid? resolvedTenantId = null;
        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            resolvedTenantId = tenantId;
        }

        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        currentUser.SetAuthSubject(subject);

        // Fallback: JWT carried no tenant_id but we DO have an authenticated
        // subject — look up their membership. Keeps the bootstrap path
        // (POST /me/join-demo-tenant) working without the operator having
        // to re-sign in immediately.
        if (resolvedTenantId is null && !string.IsNullOrWhiteSpace(subject))
        {
            var membership = await db.TenantMemberships
                .AsNoTracking()
                .Where(m => m.AuthSubject == subject)
                .Select(m => (Guid?)m.TenantId)
                .FirstOrDefaultAsync(context.RequestAborted);

            if (membership is not null)
            {
                resolvedTenantId = membership;
            }
        }

        currentTenant.SetTenantId(resolvedTenantId);

        await _next(context);
    }
}
