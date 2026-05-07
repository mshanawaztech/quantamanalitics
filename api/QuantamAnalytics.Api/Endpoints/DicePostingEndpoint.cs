using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.JobBoards;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Recruiter-facing handoff for posting a tenant's job to Dice. POST
/// "submits" the job through <see cref="IDicePostingClient"/> (a stub
/// today, real Dice partner-API call in Phase 5) and returns the posting
/// id + URL. GET <c>/preview</c> returns the same payload without making
/// a submission — useful for the SPA to show recruiters what they're
/// about to push before they confirm.
/// </summary>
/// <remarks>
/// Phase 4 has no DB persistence for Dice postings — the recruiter
/// captures the URL in their UI and goes. Persistence + a state machine
/// land in Phase 5 alongside the real provider call. Skipping the schema
/// here keeps the PR small and avoids a migration churn for what's
/// otherwise a transient stub-call wrapper.
/// </remarks>
public static class DicePostingEndpoint
{
    public static IEndpointRouteBuilder MapDicePostingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/job-boards/dice")
            .WithTags("Job Boards · Dice")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        group.MapPost("/postings/{jobId:guid}", SubmitAsync);
        group.MapGet("/preview/{jobId:guid}", PreviewAsync);

        return app;
    }

    private static async Task<Results<Ok<DicePostingResponse>, NotFound, ProblemHttpResult>> SubmitAsync(
        Guid jobId,
        AppDbContext db,
        ICurrentTenant currentTenant,
        IDicePostingClient dice,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveJobForCurrentTenantAsync(jobId, db, currentTenant, cancellationToken);
        if (resolved is null)
        {
            return TenantOrJobNotFound(currentTenant);
        }

        var (job, tenantName) = resolved.Value;

        var result = dice.Submit(new DicePostingRequest(
            TenantId: job.TenantId,
            JobId: job.Id,
            TenantName: tenantName,
            JobTitle: job.Title,
            JobSlug: job.Slug,
            JobLocation: job.Location,
            JobDescription: job.Description));

        return TypedResults.Ok(new DicePostingResponse(
            JobId: job.Id,
            JobTitle: job.Title,
            PostingId: result.PostingId,
            PostingUrl: result.PostingUrl,
            SubmittedAtUtc: DateTimeOffset.UtcNow,
            IsPreview: false));
    }

    private static async Task<Results<Ok<DicePostingResponse>, NotFound, ProblemHttpResult>> PreviewAsync(
        Guid jobId,
        AppDbContext db,
        ICurrentTenant currentTenant,
        IDicePostingClient dice,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveJobForCurrentTenantAsync(jobId, db, currentTenant, cancellationToken);
        if (resolved is null)
        {
            return TenantOrJobNotFound(currentTenant);
        }

        var (job, tenantName) = resolved.Value;

        // Same payload shape but flagged as a preview. The stub is pure so
        // calling it again here is fine — when the real Dice client lands
        // it must NOT make an actual provider call on a preview path.
        var result = dice.Submit(new DicePostingRequest(
            TenantId: job.TenantId,
            JobId: job.Id,
            TenantName: tenantName,
            JobTitle: job.Title,
            JobSlug: job.Slug,
            JobLocation: job.Location,
            JobDescription: job.Description));

        return TypedResults.Ok(new DicePostingResponse(
            JobId: job.Id,
            JobTitle: job.Title,
            PostingId: result.PostingId,
            PostingUrl: result.PostingUrl,
            SubmittedAtUtc: null,
            IsPreview: true));
    }

    /// <summary>
    /// Looks up the job in the current tenant's scope (global query filter
    /// applies — no IgnoreQueryFilters, no manual tenant predicate). Also
    /// pulls the tenant name in one round trip so the stub can include it
    /// in the request payload. Returns <c>null</c> if either lookup fails.
    /// </summary>
    private static async Task<(Job Job, string TenantName)?> ResolveJobForCurrentTenantAsync(
        Guid jobId,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return null;
        }

        var job = await db.Jobs
            .SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        // Tenant table is not tenant-scoped — no global filter to worry about.
        var tenantName = await db.Tenants
            .Where(x => x.Id == currentTenant.TenantId.Value)
            .Select(x => x.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return tenantName is null ? null : (job, tenantName);
    }

    private static Results<Ok<DicePostingResponse>, NotFound, ProblemHttpResult> TenantOrJobNotFound(ICurrentTenant currentTenant)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Posting a job to Dice requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        return TypedResults.NotFound();
    }
}

public sealed record DicePostingResponse(
    Guid JobId,
    string JobTitle,
    string PostingId,
    string PostingUrl,
    DateTimeOffset? SubmittedAtUtc,
    bool IsPreview);
