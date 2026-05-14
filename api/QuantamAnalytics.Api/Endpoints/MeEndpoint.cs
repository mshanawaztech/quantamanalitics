using System.Security.Claims;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Common;

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
        app.MapGet("/me", (ClaimsPrincipal user) =>
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
                TenantId: user.FindFirstValue(Roles.TenantIdClaim)));
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
