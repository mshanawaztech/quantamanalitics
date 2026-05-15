using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Auth;

/// <summary>
/// Phase 9 / Story 70 — resolves a tenant from the
/// <c>Authorization: Bearer qa_live_…</c> header on the public
/// platform API surface. Only runs for paths starting with
/// <c>/api/v1/public</c>; everything else flows through the existing
/// JWT pipeline untouched.
///
/// On success: sets <see cref="ICurrentTenantSetter.SetTenantId"/> and
/// records a best-effort LastUsedAtUtc on the key (rate-limited to
/// once per minute per key by the aggregate).
///
/// On failure: short-circuits with 401 and a problem detail body so the
/// public API never proxies through to a JWT handler.
/// </summary>
public sealed class PlatformApiKeyMiddleware
{
    private const string PublicPrefix = "/api/v1/public";
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next;

    public PlatformApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        AppDbContext db,
        ICurrentTenantSetter currentTenant)
    {
        if (!context.Request.Path.StartsWithSegments(PublicPrefix))
        {
            await _next(context);
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith(BearerPrefix, StringComparison.Ordinal))
        {
            await WriteUnauthorizedAsync(context, "Missing Bearer credentials on platform API call.");
            return;
        }

        var raw = header[BearerPrefix.Length..].Trim();
        var hash = PlatformApiKey.HashKey(raw);

        // IgnoreQueryFilters: there is no current tenant yet, and we'll
        // set it from the key we find. The unique index on key_hash plus
        // the IsActive clamp keeps this safe.
        var key = await db.PlatformApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive);

        if (key is null)
        {
            await WriteUnauthorizedAsync(context, "Unknown or revoked platform API key.");
            return;
        }

        currentTenant.SetTenantId(key.TenantId);
        key.RecordUse(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(context.RequestAborted);

        await _next(context);
    }

    private static Task WriteUnauthorizedAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "Platform API authentication failed",
            status = 401,
            detail,
        });
    }
}
