using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Phase 9 / Story 71 — tenant admin CRUD over outbound webhook
/// subscriptions plus a read-only delivery log so a customer can debug
/// their own integration. Actual delivery (HTTP POST with HMAC-SHA256
/// signature header, retry with exponential backoff) runs in a
/// background worker — out of scope for this PR; the
/// queue-the-attempt aggregate exists so the worker has a target.
/// </summary>
public static class WebhookEndpoint
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var subs = app.MapGroup("/api/v1/admin/webhooks")
            .WithTags("Webhooks")
            .RequireAuthorization(AuthorizationPolicies.RequirePlatformAdmin);

        subs.MapGet("/", ListAsync);
        subs.MapPost("/", CreateAsync);
        subs.MapDelete("/{id:guid}", DeactivateAsync);
        subs.MapPost("/{id:guid}/reactivate", ReactivateAsync);
        subs.MapGet("/{id:guid}/deliveries", ListDeliveriesAsync);

        return app;
    }

    private static async Task<Results<Ok<WebhookListResponse>, ProblemHttpResult>> ListAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var rows = await db.WebhookSubscriptions
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new WebhookResponse(
                x.Id, x.TargetUrl, x.EventTypes, x.SecretPrefix,
                x.IsActive, x.CreatedAtUtc,
                x.LastDeliveryAttemptUtc, x.LastSuccessfulDeliveryUtc,
                x.ConsecutiveFailureCount))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new WebhookListResponse(rows));
    }

    private static async Task<Results<Created<IssueWebhookResponse>, ProblemHttpResult>> CreateAsync(
        CreateWebhookRequest request,
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

        // Mint a fresh secret. Plaintext is shown once; the hash is stored.
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = "whsec_" + Convert.ToHexString(secretBytes).ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)))
            .ToLowerInvariant();
        var prefix = plaintext[..12];

        WebhookSubscription sub;
        try
        {
            sub = new WebhookSubscription(
                currentTenant.TenantId.Value,
                request.TargetUrl,
                request.EventTypes,
                hash,
                prefix,
                currentUser.AuthSubject);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(
                title: "Invalid webhook subscription",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        db.WebhookSubscriptions.Add(sub);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v1/admin/webhooks/{sub.Id}",
            new IssueWebhookResponse(
                sub.Id, sub.TargetUrl, sub.EventTypes, plaintext,
                sub.SecretPrefix, sub.CreatedAtUtc));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> DeactivateAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var sub = await db.WebhookSubscriptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (sub is null) return TypedResults.NotFound();

        sub.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ReactivateAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var sub = await db.WebhookSubscriptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (sub is null) return TypedResults.NotFound();

        sub.Reactivate();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<WebhookDeliveryListResponse>, ProblemHttpResult>> ListDeliveriesAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var rows = await db.WebhookDeliveries
            .Where(x => x.SubscriptionId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .Select(x => new WebhookDeliveryResponse(
                x.Id, x.EventType, x.Status.ToString(),
                x.AttemptCount, x.CreatedAtUtc,
                x.LastAttemptedAtUtc, x.NextAttemptAtUtc,
                x.LastResponseStatusCode))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new WebhookDeliveryListResponse(rows));
    }

    private static ProblemHttpResult TenantRequired() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record WebhookResponse(
    Guid Id, string TargetUrl, string EventTypes, string SecretPrefix,
    bool IsActive, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastDeliveryAttemptUtc,
    DateTimeOffset? LastSuccessfulDeliveryUtc,
    int ConsecutiveFailureCount);

public sealed record WebhookListResponse(WebhookResponse[] Items);

public sealed record CreateWebhookRequest(string TargetUrl, string EventTypes);

public sealed record IssueWebhookResponse(
    Guid Id, string TargetUrl, string EventTypes, string Secret,
    string SecretPrefix, DateTimeOffset CreatedAtUtc);

public sealed record WebhookDeliveryResponse(
    Guid Id, string EventType, string Status, int AttemptCount,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? LastAttemptedAtUtc,
    DateTimeOffset NextAttemptAtUtc, int? LastResponseStatusCode);

public sealed record WebhookDeliveryListResponse(WebhookDeliveryResponse[] Items);
