using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Phase 8 / Story 61 — recycle-bin surface for soft-deleted records.
///
/// The global EF query filter hides deleted rows from every normal read,
/// so the only way to surface them is to call <c>IgnoreQueryFilters()</c>
/// explicitly. When we do that, the global tenant clamp goes away too —
/// so each endpoint here re-applies its own <c>TenantId == currentTenant</c>
/// guard. Skipping that would leak deleted rows across tenants, which is
/// the exact failure mode soft delete is supposed to make impossible.
/// </summary>
public static class RecycleBinEndpoint
{
    /// <summary>
    /// Restore window for jobs. Aligned with the published retention claim
    /// on the trust center ("read-only export available for 30 days").
    /// Past this window a job is treated as permanently gone — endpoints
    /// won't list it and the restore call will 410.
    /// </summary>
    private static readonly TimeSpan JobRestoreWindow = TimeSpan.FromDays(30);

    public static IEndpointRouteBuilder MapRecycleBinEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recruiter/recycle-bin")
            .WithTags("Recycle bin")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        group.MapGet("/jobs", ListDeletedJobsAsync);
        group.MapPost("/jobs/{jobId:guid}/restore", RestoreJobAsync);
        group.MapDelete("/jobs/{jobId:guid}", SoftDeleteJobAsync);

        return app;
    }

    private static async Task<Results<Ok<RecycleBinJobsResponse>, ProblemHttpResult>> ListDeletedJobsAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var tenantId = currentTenant.TenantId.Value;
        var cutoffUtc = DateTimeOffset.UtcNow.Subtract(JobRestoreWindow);

        // IgnoreQueryFilters drops both the tenant clamp AND the soft-delete
        // clamp; we re-add the tenant clamp here explicitly, then filter to
        // the still-restorable subset.
        var rows = await db.Jobs
            .IgnoreQueryFilters()
            .Where(j => j.TenantId == tenantId &&
                        j.IsDeleted &&
                        j.DeletedAtUtc != null &&
                        j.DeletedAtUtc >= cutoffUtc)
            .OrderByDescending(j => j.DeletedAtUtc)
            .Select(j => new RecycleBinJobItem(
                j.Id,
                j.Title,
                j.Slug,
                j.DeletedAtUtc!.Value,
                j.DeletedByAuthSubject))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new RecycleBinJobsResponse(rows, rows.Length));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> SoftDeleteJobAsync(
        Guid jobId,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return SubjectRequired();
        }

        // No IgnoreQueryFilters here: a fresh delete only ever targets a
        // currently-visible row, so the normal filter is exactly what we want.
        var job = await db.Jobs
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
        {
            return TypedResults.NotFound();
        }

        job.SoftDelete(currentUser.AuthSubject, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<RecycleBinJobItem>, NotFound, ProblemHttpResult>> RestoreJobAsync(
        Guid jobId,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var tenantId = currentTenant.TenantId.Value;

        var job = await db.Jobs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                j => j.Id == jobId && j.TenantId == tenantId && j.IsDeleted,
                cancellationToken);

        if (job is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            job.Restore(DateTimeOffset.UtcNow, JobRestoreWindow);
        }
        catch (InvalidOperationException ex)
        {
            // Past the window → 410 Gone, distinct from 404 (still soft-
            // deleted, but no longer recoverable through this surface).
            return TypedResults.Problem(
                title: "Job no longer recoverable",
                detail: ex.Message,
                statusCode: StatusCodes.Status410Gone);
        }

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RecycleBinJobItem(
            job.Id,
            job.Title,
            job.Slug,
            DeletedAtUtc: DateTimeOffset.MinValue,
            DeletedByAuthSubject: null));
    }

    private static ProblemHttpResult TenantRequired() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            detail: "Recycle-bin operations require a tenant_id claim in the authenticated session.",
            statusCode: StatusCodes.Status412PreconditionFailed);

    private static ProblemHttpResult SubjectRequired() =>
        TypedResults.Problem(
            title: "Authenticated tenant subject required",
            detail: "Soft-delete operations require an authenticated subject so the audit trail names the deleter.",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record RecycleBinJobsResponse(RecycleBinJobItem[] Items, int Count);

public sealed record RecycleBinJobItem(
    Guid Id,
    string Title,
    string Slug,
    DateTimeOffset DeletedAtUtc,
    string? DeletedByAuthSubject);
