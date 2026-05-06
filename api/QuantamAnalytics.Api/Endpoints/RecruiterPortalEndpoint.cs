using System.Globalization;
using System.Text;
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
        group.MapGet("/invoice-ready", GetInvoiceReadyAsync);
        group.MapGet("/invoice-handoff", GetInvoiceHandoffAsync);
        group.MapGet("/invoice-handoff/quickbooks.csv", DownloadQuickBooksCsvAsync);
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

    private static async Task<Results<Ok<RecruiterInvoiceReadyResponse>, ProblemHttpResult>> GetInvoiceReadyAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var items = await db.Timesheets
            .Include(x => x.Entries)
            .Where(x => x.Status == TimesheetStatus.Approved)
            .OrderByDescending(x => x.ReviewedAtUtc ?? x.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new RecruiterInvoiceReadyResponse(ToInvoiceReadyItems(items)));
    }

    private static async Task<Results<Ok<RecruiterInvoiceHandoffResponse>, ProblemHttpResult>> GetInvoiceHandoffAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var approvedTimesheets = await db.Timesheets
            .Include(x => x.Entries)
            .Where(x => x.Status == TimesheetStatus.Approved)
            .OrderByDescending(x => x.ReviewedAtUtc ?? x.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var invoiceReadyItems = ToInvoiceReadyItems(approvedTimesheets);
        var batchReference = BuildBatchReference(currentTenant.TenantId.Value);

        return TypedResults.Ok(new RecruiterInvoiceHandoffResponse(
            $"qbo-timesheets-{DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.csv",
            invoiceReadyItems.Length,
            invoiceReadyItems.Sum(x => x.PayableHours),
            new RecruiterStripeFallbackBatchResponse(
                batchReference,
                invoiceReadyItems.Select(item => new RecruiterStripeFallbackItemResponse(
                    item.TimesheetId,
                    item.ContractorEmail,
                    $"Approved time for week of {item.WeekStartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
                    item.PayableHours,
                    "hours",
                    "send_invoice"))
                .ToArray())));
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> DownloadQuickBooksCsvAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var approvedTimesheets = await db.Timesheets
            .Include(x => x.Entries)
            .Where(x => x.Status == TimesheetStatus.Approved)
            .OrderByDescending(x => x.ReviewedAtUtc ?? x.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var invoiceReadyItems = ToInvoiceReadyItems(approvedTimesheets);
        var batchReference = BuildBatchReference(currentTenant.TenantId.Value);

        var csv = new StringBuilder();
        csv.AppendLine("batch_reference,timesheet_id,contractor_email,week_start_utc,approved_at_utc,regular_hours,overtime_hours,paid_time_off_hours,payable_hours,description");
        foreach (var item in invoiceReadyItems)
        {
            csv.Append(batchReference).Append(',')
                .Append(item.TimesheetId).Append(',')
                .Append(EscapeCsv(item.ContractorEmail)).Append(',')
                .Append(item.WeekStartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(item.ApprovedAtUtc?.ToString("O") ?? string.Empty).Append(',')
                .Append(item.RegularHours).Append(',')
                .Append(item.OvertimeHours).Append(',')
                .Append(item.PaidTimeOffHours).Append(',')
                .Append(item.PayableHours).Append(',')
                .Append(EscapeCsv($"Approved time for week of {item.WeekStartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"))
                .AppendLine();
        }

        return TypedResults.File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv; charset=utf-8",
            $"qbo-timesheets-{DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.csv");
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

    private static RecruiterJobResponse ToJobResponse(Job job) =>
        new(job.Id, job.Title, job.Slug, job.Location, job.Summary, job.Description, job.PostedOnUtc, job.IsPublished);

    private static RecruiterInvoiceReadyItemResponse[] ToInvoiceReadyItems(IEnumerable<Timesheet> timesheets) =>
        timesheets.Select(x =>
        {
            var totals = x.CalculateTotals();
            return new RecruiterInvoiceReadyItemResponse(
                x.Id,
                x.ContractorEmail,
                x.WeekStartUtc,
                x.ReviewedAtUtc,
                totals.RegularHours,
                totals.OvertimeHours,
                totals.PaidTimeOffHours,
                totals.PayableHours);
        }).ToArray();

    private static string BuildBatchReference(Guid tenantId) =>
        $"tenant-{tenantId.ToString("N")[..8]}-{DateTime.UtcNow:yyyyMMdd}";

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

    private static string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
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

public sealed record RecruiterInvoiceReadyResponse(RecruiterInvoiceReadyItemResponse[] Items);

public sealed record RecruiterInvoiceHandoffResponse(
    string QuickBooksFileName,
    int ApprovedTimesheetCount,
    decimal TotalPayableHours,
    RecruiterStripeFallbackBatchResponse StripeFallback);

public sealed record RecruiterStripeFallbackBatchResponse(
    string BatchReference,
    RecruiterStripeFallbackItemResponse[] Items);

public sealed record RecruiterStripeFallbackItemResponse(
    Guid TimesheetId,
    string ContractorEmail,
    string Description,
    decimal Quantity,
    string Unit,
    string CollectionMethod);

public sealed record RecruiterInvoiceReadyItemResponse(
    Guid TimesheetId,
    string ContractorEmail,
    DateOnly WeekStartUtc,
    DateTimeOffset? ApprovedAtUtc,
    decimal RegularHours,
    decimal OvertimeHours,
    decimal PaidTimeOffHours,
    decimal PayableHours);
