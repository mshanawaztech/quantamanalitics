using System.Security.Claims;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Returns the current user's identity as derived from the validated JWT.
/// First protected endpoint — useful sanity check for the auth wiring and
/// a starting point for the SPA's "who am I?" call after login.
/// </summary>
public static class MeEndpoint
{
    public static IEndpointRouteBuilder MapMeEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (ClaimsPrincipal user, ICurrentTenant currentTenant) =>
        {
            var roles = user.FindAll(Roles.RolesClaim).Select(c => c.Value).ToArray();

            return Results.Ok(new MeResponse(
                Sub: user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                Email: user.FindFirstValue(ClaimTypes.Email)
                       ?? user.FindFirstValue("email")
                       ?? string.Empty,
                Name: user.FindFirstValue("name") ?? string.Empty,
                Roles: roles,
                Permissions: PlatformPermissions.Expand(roles),
                // Report the RESOLVED tenant (validated against the DB by
                // TenantResolutionMiddleware), not the raw JWT claim. A stale
                // claim from a previous database resolves to null here, which
                // is what lets the SPA's bootstrap banner offer "Join demo"
                // instead of hiding behind a phantom tenant id.
                TenantId: currentTenant.TenantId?.ToString()));
        })
        .WithName("Me")
        .WithTags("System")
        .RequireAuthorization();

        return app;
    }
}

/// <summary>
/// Stable shape consumed by the Angular AuthService. Don't rename fields
/// without updating client/src/app/core/auth/auth.service.ts.
/// </summary>
public sealed record MeResponse(
    string Sub,
    string Email,
    string Name,
    string[] Roles,
    string[] Permissions,
    string? TenantId);
