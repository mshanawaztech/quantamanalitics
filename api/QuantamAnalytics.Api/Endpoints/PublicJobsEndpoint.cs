using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;

namespace QuantamAnalytics.Api.Endpoints;

public static class PublicJobsEndpoint
{
    public static IEndpointRouteBuilder MapPublicJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/jobs")
            .WithTags("Public Jobs")
            .AllowAnonymous();

        group.MapGet("/", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var jobs = await db.Jobs
                .IgnoreQueryFilters()
                .Where(x => x.IsPublished)
                .OrderByDescending(x => x.PostedOnUtc)
                .Select(x => new PublicJobListItemResponse(
                    x.Id,
                    x.Title,
                    x.Slug,
                    x.Location,
                    x.Summary,
                    x.PostedOnUtc))
                .ToArrayAsync(cancellationToken);

            return Results.Ok(jobs);
        });

        group.MapGet("/{slug}", async (string slug, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var job = await db.Jobs
                .IgnoreQueryFilters()
                .Where(x => x.IsPublished && x.Slug == slug)
                .Select(x => new PublicJobDetailResponse(
                    x.Id,
                    x.Title,
                    x.Slug,
                    x.Location,
                    x.Summary,
                    x.Description,
                    x.PostedOnUtc))
                .SingleOrDefaultAsync(cancellationToken);

            return job is null ? Results.NotFound() : Results.Ok(job);
        });

        group.MapPost("/{slug}/apply", async (
            string slug,
            PublicJobApplicationRequest request,
            AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var job = await db.Jobs
                .IgnoreQueryFilters()
                .Where(x => x.IsPublished && x.Slug == slug)
                .SingleOrDefaultAsync(cancellationToken);

            if (job is null)
            {
                return Results.NotFound();
            }

            var existing = await db.CandidateProfiles
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    x => x.TenantId == job.TenantId && x.Email == normalizedEmail,
                    cancellationToken);

            if (existing is null)
            {
                existing = new CandidateProfile(
                    job.TenantId,
                    $"guest|{Guid.CreateVersion7()}",
                    request.Email,
                    request.FullName);
                db.CandidateProfiles.Add(existing);
                await db.SaveChangesAsync(cancellationToken);
            }

            var duplicate = await db.Applications
                .IgnoreQueryFilters()
                .AnyAsync(
                    x => x.TenantId == job.TenantId &&
                         x.JobId == job.Id &&
                         x.CandidateProfileId == existing.Id,
                    cancellationToken);

            if (!duplicate)
            {
                var application = new Application(
                    job.TenantId,
                    job.Id,
                    existing.Id,
                    normalizedEmail,
                    request.FullName,
                    request.Note);

                db.Applications.Add(application);
                db.ApplicationTimelineEvents.Add(new ApplicationTimelineEvent(
                    job.TenantId,
                    application.Id,
                    existing.Id,
                    ApplicationTimelineEventType.Applied,
                    ApplicationTimelineAudience.CandidateAndRecruiter,
                    "Application received",
                    "The candidate completed the public application flow.",
                    request.FullName,
                    application.AppliedAtUtc));
                await db.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(new PublicJobApplicationResponse(
                job.Id,
                job.Slug,
                existing.Email,
                duplicate ? "Application already received for this role." : "Application received."));
        });

        return app;
    }
}

public sealed record PublicJobListItemResponse(
    Guid Id,
    string Title,
    string Slug,
    string Location,
    string Summary,
    DateOnly PostedOnUtc);

public sealed record PublicJobDetailResponse(
    Guid Id,
    string Title,
    string Slug,
    string Location,
    string Summary,
    string Description,
    DateOnly PostedOnUtc);

public sealed record PublicJobApplicationRequest(
    string FullName,
    string Email,
    string? Note);

public sealed record PublicJobApplicationResponse(
    Guid JobId,
    string JobSlug,
    string Email,
    string Message);
