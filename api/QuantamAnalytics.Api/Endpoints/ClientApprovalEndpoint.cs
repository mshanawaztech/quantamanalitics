using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

public static class ClientApprovalEndpoint
{
    public static IEndpointRouteBuilder MapClientApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/client/approvals")
            .WithTags("Client Approval")
            .RequireAuthorization(AuthorizationPolicies.RequireTimeApprovalAccess);

        group.MapGet("/timesheets", GetTimesheetsAsync);
        group.MapPost("/timesheets/{timesheetId:guid}/approve", ApproveAsync);
        group.MapPost("/timesheets/{timesheetId:guid}/reject", RejectAsync);

        return app;
    }

    private static async Task<Results<Ok<ClientApprovalTimesheetsResponse>, ProblemHttpResult>> GetTimesheetsAsync(
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
            .Where(x => x.Status != TimesheetStatus.Draft)
            .OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .Select(x => new ClientApprovalTimesheetResponse(
                x.Id,
                x.ContractorAuthSubject,
                x.ContractorEmail,
                x.WeekStartUtc,
                x.Status.ToString(),
                x.SubmittedAtUtc,
                x.ReviewedAtUtc,
                x.ReviewNote,
                x.Entries.Sum(entry => entry.Hours),
                x.Entries
                    .OrderBy(entry => entry.WorkDate)
                    .ThenBy(entry => entry.EntryType)
                    .Select(entry => new ClientApprovalTimeEntryResponse(
                        entry.WorkDate,
                        entry.Hours,
                        entry.EntryType.ToString(),
                        entry.Notes))
                    .ToArray()))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new ClientApprovalTimesheetsResponse(items));
    }

    private static async Task<Results<Ok<ClientApprovalTimesheetResponse>, NotFound, ProblemHttpResult>> ApproveAsync(
        Guid timesheetId,
        ReviewTimesheetRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var timesheet = await db.Timesheets
            .Include(x => x.Entries)
            .SingleOrDefaultAsync(x => x.Id == timesheetId, cancellationToken);

        if (timesheet is null)
        {
            return TypedResults.NotFound();
        }

        var reviewer = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(reviewer))
        {
            return TypedResults.Problem(
                title: "Reviewer identity missing",
                detail: "Authenticated review actions require a subject claim.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        try
        {
            timesheet.Approve(reviewer, request.ReviewNote);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                title: "Timesheet approval blocked",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(timesheet));
    }

    private static async Task<Results<Ok<ClientApprovalTimesheetResponse>, NotFound, ProblemHttpResult>> RejectAsync(
        Guid timesheetId,
        ReviewTimesheetRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TenantRequired();
        }

        var timesheet = await db.Timesheets
            .Include(x => x.Entries)
            .SingleOrDefaultAsync(x => x.Id == timesheetId, cancellationToken);

        if (timesheet is null)
        {
            return TypedResults.NotFound();
        }

        var reviewer = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(reviewer))
        {
            return TypedResults.Problem(
                title: "Reviewer identity missing",
                detail: "Authenticated review actions require a subject claim.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(request.ReviewNote))
        {
            return TypedResults.Problem(
                title: "Review note required",
                detail: "Rejections require a note so the contractor knows what to fix.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            timesheet.Reject(reviewer, request.ReviewNote);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                title: "Timesheet rejection blocked",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(timesheet));
    }

    private static ClientApprovalTimesheetResponse ToResponse(Timesheet timesheet) =>
        new(
            timesheet.Id,
            timesheet.ContractorAuthSubject,
            timesheet.ContractorEmail,
            timesheet.WeekStartUtc,
            timesheet.Status.ToString(),
            timesheet.SubmittedAtUtc,
            timesheet.ReviewedAtUtc,
            timesheet.ReviewNote,
            timesheet.Entries.Sum(x => x.Hours),
            timesheet.Entries
                .OrderBy(x => x.WorkDate)
                .ThenBy(x => x.EntryType)
                .Select(x => new ClientApprovalTimeEntryResponse(
                    x.WorkDate,
                    x.Hours,
                    x.EntryType.ToString(),
                    x.Notes))
                .ToArray());

    private static ProblemHttpResult TenantRequired() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            detail: "Client approval requires a tenant_id claim in the authenticated session.",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record ReviewTimesheetRequest(string? ReviewNote);

public sealed record ClientApprovalTimesheetsResponse(ClientApprovalTimesheetResponse[] Items);

public sealed record ClientApprovalTimesheetResponse(
    Guid Id,
    string ContractorAuthSubject,
    string ContractorEmail,
    DateOnly WeekStartUtc,
    string Status,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewNote,
    decimal TotalHours,
    ClientApprovalTimeEntryResponse[] Entries);

public sealed record ClientApprovalTimeEntryResponse(
    DateOnly WorkDate,
    decimal Hours,
    string EntryType,
    string? Notes);
