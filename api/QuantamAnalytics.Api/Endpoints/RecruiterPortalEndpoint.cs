using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

public static class RecruiterPortalEndpoint
{
    public static IEndpointRouteBuilder MapRecruiterPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recruiter")
            .WithTags("Recruiter Portal")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        group.MapGet("/jobs", GetJobsAsync);
        group.MapPost("/jobs", CreateJobAsync);
        group.MapPut("/jobs/{jobId:guid}", UpdateJobAsync);

        group.MapGet("/applications", GetApplicationsAsync);
        group.MapPost("/applications/{applicationId:guid}/status", UpdateApplicationStatusAsync);

        return app;
    }

    private static async Task<Results<Ok<RecruiterJobResponse[]>, ProblemHttpResult>> GetJobsAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var jobs = await db.Jobs
            .OrderByDescending(x => x.PostedOnUtc)
            .Select(x => new RecruiterJobResponse(
                x.Id,
                x.Title,
                x.Slug,
                x.Location,
                x.Summary,
                x.Description,
                x.PostedOnUtc,
                x.IsPublished))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(jobs);
    }

    private static async Task<Results<Ok<RecruiterJobResponse>, ProblemHttpResult>> CreateJobAsync(
        UpsertRecruiterJobRequest request,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var slug = Slugify(request.Title);
        if (await db.Jobs.AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            slug = $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 9, 160)];
        }

        var job = new Job(
            currentTenant.TenantId.Value,
            request.Title,
            slug,
            request.Location,
            request.Summary,
            request.Description,
            request.PostedOnUtc ?? DateOnly.FromDateTime(DateTime.UtcNow.Date));

        if (!request.IsPublished)
        {
            job.Unpublish();
        }

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ToJobResponse(job));
    }

    private static async Task<Results<Ok<RecruiterJobResponse>, NotFound, ProblemHttpResult>> UpdateJobAsync(
        Guid jobId,
        UpsertRecruiterJobRequest request,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var job = await db.Jobs.SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        job.UpdateDetails(
            request.Title,
            request.Location,
            request.Summary,
            request.Description,
            request.PostedOnUtc ?? job.PostedOnUtc);

        if (request.IsPublished)
        {
            job.Publish();
        }
        else
        {
            job.Unpublish();
        }

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ToJobResponse(job));
    }

    private static async Task<Results<Ok<RecruiterApplicationsBoardResponse>, ProblemHttpResult>> GetApplicationsAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        await EnsureSeedApplicationsAsync(db, currentTenant, cancellationToken);

        var items = await db.Applications
            .OrderByDescending(x => x.AppliedAtUtc)
            .Join(
                db.Jobs,
                application => application.JobId,
                job => job.Id,
                (application, job) => new RecruiterApplicationResponse(
                    application.Id,
                    job.Id,
                    job.Title,
                    application.CandidateName,
                    application.CandidateEmail,
                    application.Note,
                    application.Status.ToString(),
                    application.AppliedAtUtc,
                    application.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new RecruiterApplicationsBoardResponse(items));
    }

    private static async Task<Results<Ok<RecruiterApplicationResponse>, NotFound, ProblemHttpResult>> UpdateApplicationStatusAsync(
        Guid applicationId,
        UpdateApplicationStatusRequest request,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var application = await db.Applications
            .SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return TypedResults.NotFound();
        }

        switch (request.Status)
        {
            case "Interviewing":
                application.TransitionToInterviewing();
                break;
            case "OfferSent":
                application.TransitionToOfferSent();
                break;
            case "Hired":
                application.TransitionToHired();
                break;
            case "Rejected":
                application.TransitionToRejected();
                break;
            default:
                return TypedResults.Problem(
                    title: "Unsupported application status",
                    detail: "Use Interviewing, OfferSent, Hired, or Rejected.",
                    statusCode: StatusCodes.Status400BadRequest);
        }

        await db.SaveChangesAsync(cancellationToken);

        var job = await db.Jobs.SingleAsync(x => x.Id == application.JobId, cancellationToken);
        return TypedResults.Ok(new RecruiterApplicationResponse(
            application.Id,
            job.Id,
            job.Title,
            application.CandidateName,
            application.CandidateEmail,
            application.Note,
            application.Status.ToString(),
            application.AppliedAtUtc,
            application.UpdatedAtUtc));
    }

    internal static async Task EnsureSeedApplicationsAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (await db.Applications.AnyAsync(cancellationToken))
        {
            return;
        }

        var jobs = await db.Jobs.OrderByDescending(x => x.PostedOnUtc).Take(2).ToArrayAsync(cancellationToken);
        if (jobs.Length == 0 || currentTenant.TenantId is null)
        {
            return;
        }

        var profiles = new[]
        {
            new CandidateProfile(currentTenant.TenantId.Value, "seed|candidate-1", "candidate.one@example.com", "Candidate One"),
            new CandidateProfile(currentTenant.TenantId.Value, "seed|candidate-2", "candidate.two@example.com", "Candidate Two")
        };

        profiles[0].UpdateProfile(profiles[0].Email, profiles[0].FullName, "+1 555 010 1001", "Cloud recruiter", "Interested in platform recruiting roles.");
        profiles[1].UpdateProfile(profiles[1].Email, profiles[1].FullName, "+1 555 010 1002", "Data sourcer", "Brings hybrid sourcing and coordination experience.");

        db.CandidateProfiles.AddRange(profiles);
        await db.SaveChangesAsync(cancellationToken);

        var applications = new List<Application>
        {
            new(
                currentTenant.TenantId.Value,
                jobs[0].Id,
                profiles[0].Id,
                profiles[0].Email,
                profiles[0].FullName ?? "Candidate One",
                "Ready for a recruiter screening this week."),
            new(
                currentTenant.TenantId.Value,
                jobs.Length > 1 ? jobs[1].Id : jobs[0].Id,
                profiles[1].Id,
                profiles[1].Email,
                profiles[1].FullName ?? "Candidate Two",
                "Strong fit for cloud and data coordination.")
        };

        applications[1].TransitionToInterviewing();

        db.Applications.AddRange(applications);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static RecruiterJobResponse ToJobResponse(Job job) =>
        new(job.Id, job.Title, job.Slug, job.Location, job.Summary, job.Description, job.PostedOnUtc, job.IsPublished);

    private static ProblemHttpResult TenantRequired() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            detail: "Recruiter workflow requires a tenant_id claim in the authenticated session.",
            statusCode: StatusCodes.Status412PreconditionFailed);

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        return string.Join(
            '-',
            new string(chars)
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}

public sealed record UpsertRecruiterJobRequest(
    string Title,
    string Location,
    string Summary,
    string Description,
    bool IsPublished,
    DateOnly? PostedOnUtc);

public sealed record UpdateApplicationStatusRequest(string Status);

public sealed record RecruiterJobResponse(
    Guid Id,
    string Title,
    string Slug,
    string Location,
    string Summary,
    string Description,
    DateOnly PostedOnUtc,
    bool IsPublished);

public sealed record RecruiterApplicationResponse(
    Guid Id,
    Guid JobId,
    string JobTitle,
    string CandidateName,
    string CandidateEmail,
    string? Note,
    string Status,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record RecruiterApplicationsBoardResponse(RecruiterApplicationResponse[] Items);
