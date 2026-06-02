using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Api.Auth;

/// <summary>
/// Promotes any authenticated user whose email matches the configured
/// platform-owner allowlist (<c>Auth:PlatformOwnerEmails</c> — comma-separated)
/// to <see cref="Roles.PlatformAdmin"/>. Lets the platform owner demo the
/// full app without Auth0 role wiring or a tenant-level admin UI.
/// </summary>
/// <remarks>
/// Runs on every authenticated request via the ASP.NET Core
/// <see cref="IClaimsTransformation"/> pipeline, so the role lands on the
/// <see cref="ClaimsPrincipal"/> BEFORE authorization policies (e.g.
/// <c>RequireRecruitingAccess</c>) check <c>RequireRole(...)</c>. The role
/// is appended idempotently — never duplicated if already present.
/// Empty config = transformer is a no-op (no behavior change).
/// </remarks>
public sealed class PlatformOwnerClaimsTransformer : IClaimsTransformation
{
    /// <summary>
    /// Configuration key. As an environment variable on a Container App:
    /// <c>Auth__PlatformOwnerEmails</c>. Value: a comma-separated list of
    /// lowercased email addresses, e.g.
    /// <c>owner@example.com,co-founder@example.com</c>.
    /// </summary>
    public const string ConfigKey = "Auth:PlatformOwnerEmails";

    private readonly string[] _ownerEmails;

    public PlatformOwnerClaimsTransformer(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration[ConfigKey] ?? string.Empty;
        _ownerEmails = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToArray();
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (_ownerEmails.Length == 0)
        {
            return Task.FromResult(principal);
        }

        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult(principal);
        }

        if (!_ownerEmails.Contains(email.Trim().ToLowerInvariant()))
        {
            return Task.FromResult(principal);
        }

        // Already PlatformAdmin → no-op so re-runs of this transformer (the
        // pipeline can call it more than once per request) don't pile up the
        // same claim.
        if (principal.IsInRole(Roles.PlatformAdmin))
        {
            return Task.FromResult(principal);
        }

        identity.AddClaim(new Claim(Roles.RolesClaim, Roles.PlatformAdmin));
        return Task.FromResult(principal);
    }
}
