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
            await EnsureSeedJobsAsync(db, cancellationToken);

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
            await EnsureSeedJobsAsync(db, cancellationToken);

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

        return app;
    }

    private static async Task EnsureSeedJobsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Jobs.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            return;
        }

        var tenant = await db.Tenants.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            return;
        }

        db.Jobs.AddRange(
            new Job(
                tenant.Id,
                "Senior .NET Staffing Solutions Lead",
                "senior-dotnet-staffing-solutions-lead",
                "Remote · United States",
                "Own recruiter collaboration, client intake, and candidate workflow design for a growing staffing operation.",
                "Lead the shaping of recruiter-facing workflow inside a modern staffing platform. You will partner with delivery leadership, turn recruiting process pain into software requirements, and help operationalize better candidate submission velocity.",
                DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-4))),
            new Job(
                tenant.Id,
                "Technical Recruiter - Cloud & Data",
                "technical-recruiter-cloud-and-data",
                "Dallas, TX · Hybrid",
                "Drive sourcing and screening for cloud, data, and engineering roles while improving reusable search playbooks.",
                "Join a staffing team focused on cloud and data talent. This role blends hands-on sourcing, hiring-manager calibration, candidate storytelling, and lightweight process improvement across the submission funnel.",
                DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1))));

        await db.SaveChangesAsync(cancellationToken);
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
