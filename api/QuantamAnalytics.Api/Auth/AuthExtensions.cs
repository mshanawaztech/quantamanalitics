using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Api.Auth;

/// <summary>
/// Single entry point for everything auth-related: JWT bearer validation
/// against Auth0, role-based authorization policies, and the claim mapping
/// that turns Auth0's namespaced custom claims into things ASP.NET understands.
/// </summary>
/// <remarks>
/// Designed to be safely optional. If <c>Auth0:Domain</c> and
/// <c>Auth0:Audience</c> are not configured, this is a no-op and
/// <see cref="AddPlatformAuth"/> returns false. Callers can then skip
/// <c>UseAuthentication</c>/<c>UseAuthorization</c> middleware and any
/// <c>RequireAuthorization()</c> endpoints — keeping local-dev runs and
/// pre-Auth0 deployments functional.
/// </remarks>
public static class AuthExtensions
{
    public const string Auth0DomainKey = "Auth0:Domain";
    public const string Auth0AudienceKey = "Auth0:Audience";

    /// <summary>
    /// Wires up JWT bearer authentication and role-based authorization
    /// policies if Auth0 is configured. Returns true when auth was
    /// registered, false when skipped (config missing).
    /// </summary>
    public static bool AddPlatformAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var domain = configuration[Auth0DomainKey];
        var audience = configuration[Auth0AudienceKey];

        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(audience))
        {
            // Caller decides whether to log a warning. We just signal "skipped".
            return false;
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Authority is the OIDC discovery root. Auth0 serves
                // jwks/openid-configuration off /.well-known/.
                options.Authority = $"https://{domain}/";
                options.Audience = audience;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://{domain}/",
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),

                    // Auth0 emits roles in a custom-namespace claim (set by an
                    // Action — see docs/auth.md). Tell ASP.NET to use that key
                    // for [Authorize(Roles = "...")] and User.IsInRole(...).
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = Roles.RolesClaim,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.RequirePlatformAdmin,
                p => p.RequireAuthenticatedUser().RequireRole(Roles.PlatformAdmin));

            options.AddPolicy(AuthorizationPolicies.RequireRecruiter,
                p => p.RequireAuthenticatedUser().RequireRole(Roles.Recruiter));

            options.AddPolicy(AuthorizationPolicies.RequireRecruitingAccess,
                p => p.RequireAuthenticatedUser().RequireRole(
                    Roles.Recruiter,
                    Roles.HrAdmin,
                    Roles.Manager,
                    Roles.PlatformAdmin));

            options.AddPolicy(AuthorizationPolicies.RequireInterviewAccess,
                p => p.RequireAuthenticatedUser().RequireRole(
                    Roles.Interviewer,
                    Roles.Recruiter,
                    Roles.HrAdmin,
                    Roles.Manager,
                    Roles.PlatformAdmin));

            options.AddPolicy(AuthorizationPolicies.RequireTimeApprovalAccess,
                p => p.RequireAuthenticatedUser().RequireRole(
                    Roles.Client,
                    Roles.PayrollAdmin,
                    Roles.Manager,
                    Roles.PlatformAdmin));

            options.AddPolicy(AuthorizationPolicies.RequirePayrollAccess,
                p => p.RequireAuthenticatedUser().RequireRole(
                    Roles.PayrollAdmin,
                    Roles.Manager,
                    Roles.PlatformAdmin));

            options.AddPolicy(AuthorizationPolicies.RequireClientPortalAccess,
                p => p.RequireAuthenticatedUser().RequireRole(Roles.Client, Roles.PlatformAdmin));

            options.AddPolicy(AuthorizationPolicies.RequireCandidate,
                p => p.RequireAuthenticatedUser().RequireRole(Roles.Candidate));

            // Default fallback — every endpoint without an explicit policy
            // requires an authenticated user. Public endpoints opt out with
            // .AllowAnonymous(). This is the safer default for a B2B SaaS.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        // Platform-owner email allowlist → PlatformAdmin role.
        // Runs on every authenticated request; empty config is a no-op.
        services.AddSingleton<IClaimsTransformation, PlatformOwnerClaimsTransformer>();

        return true;
    }
}
