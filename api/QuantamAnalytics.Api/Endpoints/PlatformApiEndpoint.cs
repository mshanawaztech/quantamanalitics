using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Phase 9 / Story 70 — two surfaces:
///
///   1. Tenant-facing CRUD over their API keys, gated on the existing
///      RequirePlatformAdmin role. Lives under
///      /api/v1/admin/platform-api-keys and reads/writes JWT-authenticated.
///   2. The public API itself at /api/v1/public/* — authenticated by the
///      PlatformApiKeyMiddleware. Initially exposes /jobs read so a
///      customer can mirror their open roles into their own ATS without
///      scraping.
/// </summary>
public static class PlatformApiEndpoint
{
    public static IEndpointRouteBuilder MapPlatformApiEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Key management — JWT-authenticated.
        var admin = app.MapGroup("/api/v1/admin/platform-api-keys")
            .WithTags("Platform API keys")
            .RequireAuthorization(AuthorizationPolicies.RequirePlatformAdmin);

        admin.MapGet("/", ListAsync);
        admin.MapPost("/", IssueAsync);
        admin.MapDelete("/{id:guid}", RevokeAsync);

        // 2. Public read API — authenticated by the API-key middleware.
        // No .RequireAuthorization() call; the middleware sets a tenant
        // before this runs and the global query filter clamps the result.
        app.MapGet("/api/v1/public/jobs", ListPublicJobsAsync)
            .WithTags("Public platform API");

        return app;
    }

    // ── Admin: key management ─────────────────────────────────────────
    private static async Task<Results<Ok<PlatformApiKeyListResponse>, ProblemHttpResult>> ListAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var rows = await db.PlatformApiKeys
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new PlatformApiKeyResponse(
                x.Id, x.Label, x.KeyPrefix, x.IsActive,
                x.CreatedAtUtc, x.LastUsedAtUtc))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new PlatformApiKeyListResponse(rows));
    }

    private static async Task<Results<Created<IssuePlatformApiKeyResponse>, ProblemHttpResult>> IssueAsync(
        IssuePlatformApiKeyRequest request,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return TypedResults.Problem(
                title: "Authenticated tenant subject required",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var (entity, plaintext) = PlatformApiKey.Issue(
            currentTenant.TenantId.Value,
            request.Label,
            currentUser.AuthSubject);

        db.PlatformApiKeys.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        // Plaintext key surfaced ONCE here — never persisted, never
        // recoverable. The frontend must show + confirm copy before
        // navigating away.
        return TypedResults.Created(
            $"/api/v1/admin/platform-api-keys/{entity.Id}",
            new IssuePlatformApiKeyResponse(
                Id: entity.Id,
                Label: entity.Label,
                Key: plaintext,
                KeyPrefix: entity.KeyPrefix,
                CreatedAtUtc: entity.CreatedAtUtc));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> RevokeAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var key = await db.PlatformApiKeys.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (key is null) return TypedResults.NotFound();

        key.Revoke();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // ── Public API: read jobs ─────────────────────────────────────────
    private static async Task<Results<Ok<PublicJobsResponse>, ProblemHttpResult>> ListPublicJobsAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Authentication required",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var items = await db.Jobs
            .Where(j => j.IsPublished)
            .OrderByDescending(j => j.PostedOnUtc)
            .Select(j => new PublicJobItem(
                j.Id, j.Slug, j.Title, j.Location, j.Summary, j.PostedOnUtc))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new PublicJobsResponse(items, items.Length));
    }

    private static ProblemHttpResult TenantRequired() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record PlatformApiKeyResponse(
    Guid Id, string Label, string KeyPrefix, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? LastUsedAtUtc);

public sealed record PlatformApiKeyListResponse(PlatformApiKeyResponse[] Items);

public sealed record IssuePlatformApiKeyRequest(string Label);

public sealed record IssuePlatformApiKeyResponse(
    Guid Id, string Label, string Key, string KeyPrefix,
    DateTimeOffset CreatedAtUtc);

public sealed record PublicJobsResponse(PublicJobItem[] Items, int Count);

public sealed record PublicJobItem(
    Guid Id, string Slug, string Title, string Location, string Summary,
    DateOnly PostedOnUtc);
