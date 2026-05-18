using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
public sealed partial class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    /// <summary>
    /// Source-generated log delegate — satisfies CA1848 by pre-generating
    /// the formatter at compile time. Only fires when an authenticated
    /// principal arrived without a resolvable subject claim.
    /// </summary>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Authenticated principal arrived with no resolvable subject claim. " +
                  "Claim types present: {ClaimTypes}")]
    private static partial void LogMissingSubjectClaim(ILogger logger, string claimTypes);

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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

        // Auth0 emits the auth subject as `sub`. Depending on the JWT bearer
        // claim-mapping config that may surface as ClaimTypes.NameIdentifier,
        // the raw `sub`, or the JwtRegisteredClaimNames.Sub URI. Read all
        // three so downstream consumers never see a phantom-null subject.
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue("sub");

        // Last-resort fallback: if no subject claim made it through but we
        // DO have an authenticated email, derive a stable pseudo-subject
        // from it. This unblocks demos where an Auth0 quirk (custom claim
        // mapping, missing post-login Action, token type confusion) strips
        // the sub claim from the JWT. Format mirrors Auth0 social subjects.
        if (string.IsNullOrWhiteSpace(subject))
        {
            var email = context.User.FindFirstValue(ClaimTypes.Email)
                ?? context.User.FindFirstValue("email")
                ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Email);
            if (!string.IsNullOrWhiteSpace(email))
            {
                subject = "email|" + email.Trim().ToLowerInvariant();
            }
        }

        currentUser.SetAuthSubject(subject);

        // Diagnostic: if the user is authenticated but we still couldn't
        // find a subject claim, log the claim types that DID come through
        // so we can see in App Insights / container logs exactly what
        // shape the JWT delivered. Without this it's "guess and ship".
        if (context.User.Identity?.IsAuthenticated == true &&
            string.IsNullOrWhiteSpace(subject))
        {
            var claimTypes = context.User.Claims
                .Select(c => c.Type)
                .Distinct()
                .OrderBy(t => t)
                .ToArray();
            LogMissingSubjectClaim(_logger, string.Join(", ", claimTypes));
        }

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
