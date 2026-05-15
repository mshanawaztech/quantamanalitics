using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Phase 9 / Story 72 — tenant-admin-facing billing surface.
///
/// GET /api/v1/admin/billing/summary returns the current TenantSubscription
/// plus computed signals: how many days of trial remain, whether the
/// tenant is "entitled" (the gate the rest of the platform reads), and
/// the plan label. The aggregate is written by the Phase-5 Stripe sync
/// flow (PR-43) on webhook receipt; this endpoint is read-only on the
/// happy path.
/// </summary>
public static class BillingEndpoint
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/billing")
            .WithTags("Billing")
            .RequireAuthorization(AuthorizationPolicies.RequirePlatformAdmin);

        group.MapGet("/summary", GetSummaryAsync);

        return app;
    }

    private static async Task<Results<Ok<BillingSummaryResponse>, NotFound, ProblemHttpResult>> GetSummaryAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var sub = await db.TenantSubscriptions.FirstOrDefaultAsync(cancellationToken);
        if (sub is null)
        {
            return TypedResults.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var trialDaysRemaining = sub.TrialEndsAtUtc is { } end && end > now
            ? Math.Max(0, (int)Math.Ceiling((end - now).TotalDays))
            : 0;

        return TypedResults.Ok(new BillingSummaryResponse(
            sub.PlanCode,
            sub.IncludedSeats,
            sub.PricePerSeat,
            sub.Currency,
            sub.Status.ToString(),
            sub.TrialEndsAtUtc,
            trialDaysRemaining,
            sub.CurrentPeriodEndsAtUtc,
            sub.IsEntitled(now)));
    }
}

public sealed record BillingSummaryResponse(
    string PlanCode,
    int IncludedSeats,
    decimal PricePerSeat,
    string Currency,
    string Status,
    DateTimeOffset? TrialEndsAtUtc,
    int TrialDaysRemaining,
    DateTimeOffset? CurrentPeriodEndsAtUtc,
    bool IsEntitled);
