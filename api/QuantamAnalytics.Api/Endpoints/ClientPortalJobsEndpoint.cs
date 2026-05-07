using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Read-only client surface over the recruiting pipeline. A client signed
/// into the portal sees the jobs that exist at their tenant, the
/// applications candidates have submitted to those jobs, and the
/// recruiter→client submissions (the formal candidate hand-offs).
/// </summary>
/// <remarks>
/// Tenant scoping comes from the global query filter on every
/// <see cref="ITenantScoped"/> entity — no IgnoreQueryFilters here, no
/// manual tenant predicates. A Client at tenant B asking for tenant A's
/// job id gets 404, same as the recruiter portal.
///
/// What the client sees vs. doesn't:
/// - Jobs: full detail (description, location, posted date, published flag).
/// - Applications: candidate name + email + status + applied date. The
///   recruiter-facing <c>Note</c> is intentionally omitted — it's an
///   internal recruiter scratchpad, not for client eyes.
/// - Submissions: pitch summary, status, decision date. The
///   <c>SubmittedByAuthSubject</c> is omitted — clients shouldn't see
///   which individual recruiter pushed the candidate.
/// </remarks>
public static class ClientPortalJobsEndpoint
{
    public static IEndpointRouteBuilder MapClientPortalJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/client/jobs")
            .WithTags("Client Portal · Jobs")
            .RequireAuthorization(AuthorizationPolicies.RequireClientPortalAccess);

        group.MapGet("/", ListJobsAsync);
        group.MapGet("/{jobId:guid}", GetJobAsync);
        group.MapGet("/{jobId:guid}/applications", ListApplicationsForJobAsync);
        group.MapGet("/{jobId:guid}/submissions", ListSubmissionsForJobAsync);

        return app;
    }

    private static async Task<Results<Ok<ClientPortalJobsResponse>, ProblemHttpResult>> ListJobsAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var jobs = await db.Jobs
            .OrderByDescending(x => x.PostedOnUtc)
            .Select(x => new ClientPortalJobResponse(
                x.Id,
                x.Title,
                x.Slug,
                x.Location,
                x.Summary,
                x.PostedOnUtc,
                x.IsPublished))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new ClientPortalJobsResponse(jobs));
    }

    private static async Task<Results<Ok<ClientPortalJobDetailResponse>, NotFound, ProblemHttpResult>> GetJobAsync(
        Guid jobId,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var job = await db.Jobs
            .Where(x => x.Id == jobId)
            .Select(x => new ClientPortalJobDetailResponse(
                x.Id,
                x.Title,
                x.Slug,
                x.Location,
                x.Summary,
                x.Description,
                x.PostedOnUtc,
                x.IsPublished))
            .SingleOrDefaultAsync(cancellationToken);

        return job is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(job);
    }

    private static async Task<Results<Ok<ClientPortalApplicationsResponse>, NotFound, ProblemHttpResult>> ListApplicationsForJobAsync(
        Guid jobId,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        // Confirm the job exists in the current tenant before exposing any
        // applications. Without this, a client could enumerate any UUID and
        // receive an empty list (which leaks "this id was not yours" vs
        // "this id had no applications").
        var jobExists = await db.Jobs
            .AnyAsync(x => x.Id == jobId, cancellationToken);
        if (!jobExists)
        {
            return TypedResults.NotFound();
        }

        var applications = await db.Applications
            .Where(x => x.JobId == jobId)
            .OrderByDescending(x => x.AppliedAtUtc)
            .Select(x => new ClientPortalApplicationResponse(
                x.Id,
                x.CandidateName,
                x.CandidateEmail,
                x.Status.ToString(),
                x.AppliedAtUtc,
                x.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new ClientPortalApplicationsResponse(applications));
    }

    private static async Task<Results<Ok<ClientPortalSubmissionsResponse>, NotFound, ProblemHttpResult>> ListSubmissionsForJobAsync(
        Guid jobId,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequiredProblem();
        }

        var jobExists = await db.Jobs
            .AnyAsync(x => x.Id == jobId, cancellationToken);
        if (!jobExists)
        {
            return TypedResults.NotFound();
        }

        var submissions = await db.Submissions
            .Where(x => x.JobId == jobId)
            .OrderByDescending(x => x.SubmittedToClientAtUtc ?? x.UpdatedAtUtc)
            .Select(x => new ClientPortalSubmissionResponse(
                x.Id,
                x.CandidateName,
                x.CandidateEmail,
                x.PitchSummary,
                x.ClientCompanyName,
                x.Status.ToString(),
                x.SubmittedToClientAtUtc,
                x.ClientDecisionAtUtc,
                x.ClientDecisionNote,
                x.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new ClientPortalSubmissionsResponse(submissions));
    }

    private static ProblemHttpResult TenantRequiredProblem() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            detail: "The client portal requires a tenant_id claim in the authenticated session.",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record ClientPortalJobsResponse(ClientPortalJobResponse[] Items);

public sealed record ClientPortalJobResponse(
    Guid Id,
    string Title,
    string Slug,
    string Location,
    string Summary,
    DateOnly PostedOnUtc,
    bool IsPublished);

public sealed record ClientPortalJobDetailResponse(
    Guid Id,
    string Title,
    string Slug,
    string Location,
    string Summary,
    string Description,
    DateOnly PostedOnUtc,
    bool IsPublished);

public sealed record ClientPortalApplicationsResponse(ClientPortalApplicationResponse[] Items);

public sealed record ClientPortalApplicationResponse(
    Guid Id,
    string CandidateName,
    string CandidateEmail,
    string Status,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ClientPortalSubmissionsResponse(ClientPortalSubmissionResponse[] Items);

public sealed record ClientPortalSubmissionResponse(
    Guid Id,
    string CandidateName,
    string CandidateEmail,
    string? PitchSummary,
    string? ClientCompanyName,
    string Status,
    DateTimeOffset? SubmittedToClientAtUtc,
    DateTimeOffset? ClientDecisionAtUtc,
    string? ClientDecisionNote,
    DateTimeOffset UpdatedAtUtc);
