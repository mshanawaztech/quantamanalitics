using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Baseline reporting surface for the recruiting team. Single endpoint,
/// single round-trip, returns a summary of the three metrics that justify
/// the platform's value to a staffing firm:
///
/// 1. Application funnel — counts of applications by status. Shows where
///    candidates are dropping out of the pipeline.
/// 2. Time-to-fill — average days from a job's posting date to the first
///    application reaching the <see cref="ApplicationStatus.Hired"/>
///    status. Aggregated across all hired jobs at the tenant. The median
///    is on the wishlist but harder to compute in a single SQL pass —
///    deferred to a future PR.
/// 3. Recruiter activity — submissions count per recruiter
///    (<see cref="Submission.SubmittedByAuthSubject"/>). Approximates "who
///    on the team is moving the most candidates forward". A real activity
///    score will need additional signal sources (interview scheduling,
///    application status transitions) — this PR ships the simplest cut.
/// </summary>
/// <remarks>
/// Source-of-hire is on the Phase 4 board but the current
/// <see cref="Application"/> entity has no source attribution column —
/// every application looks identical (came in via the public job board).
/// Adding a structured <c>source</c> column is a separate PR with a
/// migration; not in scope here. The summary endpoint will grow a
/// <c>SourceOfHire</c> section then.
///
/// Tenant scoping uses the global query filter on every read — same shape
/// the recruiter portal already uses, same tenant-isolation IT pattern
/// catches regressions.
/// </remarks>
public static class ReportingEndpoint
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recruiter/reports")
            .WithTags("Reporting")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        group.MapGet("/summary", GetSummaryAsync);
        group.MapGet("/burn-rate", GetBurnRateAsync);

        return app;
    }

    /// <summary>
    /// Project burn-rate: payable hours logged per week across the tenant
    /// (submitted + approved timesheets), plus a per-consultant breakdown and
    /// an estimated cost using the tenant's default hourly rate. Aggregation
    /// is in memory after a single tenant-scoped read — EF GroupBy translation
    /// against Postgres is fragile.
    /// </summary>
    private static async Task<Results<Ok<BurnRateResponse>, ProblemHttpResult>> GetBurnRateAsync(
        int? weeks,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Reporting requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var window = Math.Clamp(weeks ?? 8, 1, 52);
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-7 * window);

        var sheets = await db.Timesheets
            .Include(x => x.Entries)
            .Where(x => x.WeekStartUtc >= cutoff &&
                        (x.Status == TimesheetStatus.Submitted || x.Status == TimesheetStatus.Approved))
            .ToListAsync(cancellationToken);

        var rows = sheets
            .Select(x => new { x.WeekStartUtc, x.ContractorEmail, Hours = x.CalculateTotals().PayableHours })
            .ToList();

        var weeklySeries = rows
            .GroupBy(x => x.WeekStartUtc)
            .Select(g => new BurnRateWeek(
                g.Key,
                g.Sum(x => x.Hours),
                g.Select(x => x.ContractorEmail).Distinct().Count()))
            .OrderBy(x => x.WeekStartUtc)
            .ToArray();

        var perConsultant = rows
            .GroupBy(x => x.ContractorEmail)
            .Select(g => new BurnRateConsultant(g.Key, g.Sum(x => x.Hours)))
            .OrderByDescending(x => x.PayableHours)
            .ToArray();

        var branding = await db.TenantBrandings
            .SingleOrDefaultAsync(x => x.TenantId == currentTenant.TenantId, cancellationToken);
        var rate = branding?.DefaultHourlyRate ?? 0m;
        var currency = branding?.DefaultCurrency ?? "USD";
        var totalHours = rows.Sum(x => x.Hours);

        return TypedResults.Ok(new BurnRateResponse(
            weeklySeries,
            perConsultant,
            totalHours,
            Math.Round(totalHours * rate, 2),
            rate,
            currency));
    }

    private static async Task<Results<Ok<ReportingSummaryResponse>, ProblemHttpResult>> GetSummaryAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Reporting requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var funnel = await BuildFunnelAsync(db, cancellationToken);
        var timeToFill = await BuildTimeToFillAsync(db, cancellationToken);
        var activity = await BuildRecruiterActivityAsync(db, cancellationToken);

        return TypedResults.Ok(new ReportingSummaryResponse(funnel, timeToFill, activity));
    }

    /// <summary>
    /// Counts applications grouped by status, projected onto the response
    /// record so missing statuses render as zero rather than absent keys.
    /// </summary>
    private static async Task<ReportingFunnelResponse> BuildFunnelAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // Pull statuses (tenant filter applied by the global query filter)
        // and group in memory. EF Core's GroupBy-to-aggregate SQL
        // translation is fragile across providers/versions; the per-tenant
        // application row count is small enough that a client-side group
        // is the pragmatic, always-correct choice.
        var statuses = await db.Applications
            .Select(x => x.Status)
            .ToListAsync(cancellationToken);

        var counts = statuses
            .GroupBy(s => s)
            .ToDictionary(g => g.Key, g => g.Count());

        return new ReportingFunnelResponse(
            Applied: counts.GetValueOrDefault(ApplicationStatus.Applied),
            Interviewing: counts.GetValueOrDefault(ApplicationStatus.Interviewing),
            OfferSent: counts.GetValueOrDefault(ApplicationStatus.OfferSent),
            Hired: counts.GetValueOrDefault(ApplicationStatus.Hired),
            Rejected: counts.GetValueOrDefault(ApplicationStatus.Rejected));
    }

    /// <summary>
    /// For each job at the tenant, find the earliest hired-application's
    /// updated-at timestamp, subtract the job's posted-on date, and average
    /// the result. Server-side projection keeps a single round trip.
    /// </summary>
    private static async Task<ReportingTimeToFillResponse> BuildTimeToFillAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // Pull the raw pairs back to memory and compute the days/average in
        // C#. A pure-SQL version is possible but the snake-case naming +
        // DateOnly→TimeSpan arithmetic isn't trivial across providers, and
        // the row count is per-job-hired which is small.
        // Fetch the hired-application/job pairs with a simple join (the
        // tenant filter applies to both sides), then group + aggregate in
        // memory. The join itself translates fine; it's the trailing
        // GroupBy(...).Select(g => g.Min(...)) that EF Core can fail to
        // render. Per-job-hired row count is small, so the round trip cost
        // of pulling raw pairs is negligible.
        var pairs = await db.Applications
            .Where(x => x.Status == ApplicationStatus.Hired)
            .Join(
                db.Jobs,
                application => application.JobId,
                job => job.Id,
                (application, job) => new { job.Id, job.PostedOnUtc, HiredAtUtc = application.UpdatedAtUtc })
            .ToListAsync(cancellationToken);

        var hired = pairs
            .GroupBy(x => x.Id)
            .Select(g => new
            {
                JobId = g.Key,
                PostedOnUtc = g.Min(x => x.PostedOnUtc),
                FirstHiredAtUtc = g.Min(x => x.HiredAtUtc),
            })
            .ToArray();

        if (hired.Length == 0)
        {
            return new ReportingTimeToFillResponse(
                TotalJobsHired: 0,
                AverageDays: null);
        }

        var totalDays = 0d;
        foreach (var row in hired)
        {
            var posted = row.PostedOnUtc.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            totalDays += (row.FirstHiredAtUtc - posted).TotalDays;
        }

        return new ReportingTimeToFillResponse(
            TotalJobsHired: hired.Length,
            AverageDays: Math.Round(totalDays / hired.Length, 1));
    }

    /// <summary>
    /// Submissions grouped by the recruiter who pushed them. Sorted by
    /// volume desc so the highest-activity recruiter renders first in
    /// any UI consuming this without re-sorting.
    /// </summary>
    private static async Task<RecruiterActivityResponse[]> BuildRecruiterActivityAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // Same rationale as the funnel: pull the recruiter subjects
        // (tenant-filtered) and group in memory rather than rely on
        // EF Core translating GroupBy + Count + OrderBy on the projection.
        var subjects = await db.Submissions
            .Where(x => x.SubmittedByAuthSubject != null)
            .Select(x => x.SubmittedByAuthSubject!)
            .ToListAsync(cancellationToken);

        return subjects
            .GroupBy(s => s)
            .Select(g => new RecruiterActivityResponse(
                AuthSubject: g.Key,
                SubmissionCount: g.Count()))
            .OrderByDescending(x => x.SubmissionCount)
            .ToArray();
    }
}

public sealed record ReportingSummaryResponse(
    ReportingFunnelResponse Funnel,
    ReportingTimeToFillResponse TimeToFill,
    RecruiterActivityResponse[] RecruiterActivity);

public sealed record ReportingFunnelResponse(
    int Applied,
    int Interviewing,
    int OfferSent,
    int Hired,
    int Rejected);

public sealed record ReportingTimeToFillResponse(
    int TotalJobsHired,
    double? AverageDays);

public sealed record RecruiterActivityResponse(
    string AuthSubject,
    int SubmissionCount);

public sealed record BurnRateResponse(
    BurnRateWeek[] Weeks,
    BurnRateConsultant[] Consultants,
    decimal TotalPayableHours,
    decimal EstimatedCost,
    decimal HourlyRateUsed,
    string Currency);

public sealed record BurnRateWeek(
    DateOnly WeekStartUtc,
    decimal PayableHours,
    int Consultants);

public sealed record BurnRateConsultant(
    string Consultant,
    decimal PayableHours);
