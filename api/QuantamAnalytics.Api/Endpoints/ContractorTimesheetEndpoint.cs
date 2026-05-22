using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

public static class ContractorTimesheetEndpoint
{
    public static IEndpointRouteBuilder MapContractorTimesheetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/contractor/timesheets")
            .WithTags("Contractor Timesheets")
            .RequireAuthorization();

        group.MapGet("/", ListMineAsync);
        group.MapGet("/current", GetCurrentAsync);
        group.MapPut("/current", SaveDraftAsync);
        group.MapPost("/current/submit", SubmitAsync);

        return app;
    }

    private static async Task<Results<Ok<ContractorTimesheetListResponse>, ProblemHttpResult>> ListMineAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var session = ResolveSession(user, currentTenant.TenantId);
        if (session is null)
        {
            return TenantRequired();
        }

        var sheets = await db.Timesheets
            .Include(x => x.Entries)
            .Where(x => x.ContractorAuthSubject == session.AuthSubject)
            .OrderByDescending(x => x.WeekStartUtc)
            .ToListAsync(cancellationToken);

        var rows = sheets
            .Select(timesheet =>
            {
                var totals = timesheet.CalculateTotals();
                return new ContractorTimesheetSummaryResponse(
                    timesheet.Id,
                    timesheet.WeekStartUtc,
                    timesheet.Status.ToString(),
                    timesheet.Entries.Sum(x => x.Hours),
                    totals.PayableHours,
                    timesheet.SubmittedAtUtc,
                    timesheet.ReviewedAtUtc);
            })
            .ToArray();

        return TypedResults.Ok(new ContractorTimesheetListResponse(rows));
    }

    private static async Task<Results<Ok<ContractorTimesheetResponse>, ProblemHttpResult>> GetCurrentAsync(
        DateOnly? weekStart,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var session = ResolveSession(user, currentTenant.TenantId);
        if (session is null)
        {
            return TenantRequired();
        }

        var normalizedWeekStart = NormalizeWeekStart(weekStart ?? DateOnly.FromDateTime(DateTime.UtcNow.Date));
        var timesheet = await LoadTimesheetAsync(
            db,
            session.TenantId,
            session.AuthSubject,
            normalizedWeekStart,
            cancellationToken);

        return TypedResults.Ok(timesheet is null
            ? EmptyResponse(session, normalizedWeekStart)
            : ToResponse(timesheet));
    }

    private static async Task<Results<Ok<ContractorTimesheetResponse>, ProblemHttpResult>> SaveDraftAsync(
        UpsertContractorTimesheetRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var session = ResolveSession(user, currentTenant.TenantId);
        if (session is null)
        {
            return TenantRequired();
        }

        var parsedEntries = ParseEntries(request);
        if (parsedEntries.Error is not null)
        {
            return TypedResults.Problem(
                title: "Invalid timesheet payload",
                detail: parsedEntries.Error,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var normalizedWeekStart = NormalizeWeekStart(request.WeekStartUtc);
        var timesheet = await LoadTimesheetAsync(
            db,
            session.TenantId,
            session.AuthSubject,
            normalizedWeekStart,
            cancellationToken);

        if (timesheet is null)
        {
            timesheet = new Timesheet(
                session.TenantId,
                session.AuthSubject,
                session.Email,
                normalizedWeekStart);

            db.Timesheets.Add(timesheet);
        }

        var applyError = ApplyEntries(timesheet, parsedEntries.Entries!);
        if (applyError is not null)
        {
            return applyError;
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(timesheet));
    }

    private static async Task<Results<Ok<ContractorTimesheetResponse>, ProblemHttpResult>> SubmitAsync(
        UpsertContractorTimesheetRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        var session = ResolveSession(user, currentTenant.TenantId);
        if (session is null)
        {
            return TenantRequired();
        }

        var parsedEntries = ParseEntries(request);
        if (parsedEntries.Error is not null)
        {
            return TypedResults.Problem(
                title: "Invalid timesheet payload",
                detail: parsedEntries.Error,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var normalizedWeekStart = NormalizeWeekStart(request.WeekStartUtc);
        var timesheet = await LoadTimesheetAsync(
            db,
            session.TenantId,
            session.AuthSubject,
            normalizedWeekStart,
            cancellationToken);

        if (timesheet is null)
        {
            timesheet = new Timesheet(
                session.TenantId,
                session.AuthSubject,
                session.Email,
                normalizedWeekStart);

            db.Timesheets.Add(timesheet);
        }

        var applyError = ApplyEntries(timesheet, parsedEntries.Entries!);
        if (applyError is not null)
        {
            return applyError;
        }

        try
        {
            timesheet.Submit();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                title: "Timesheet submit blocked",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(timesheet));
    }

    private static ContractorSession? ResolveSession(ClaimsPrincipal user, Guid? tenantId)
    {
        var authSubject = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");

        if (tenantId is null ||
            string.IsNullOrWhiteSpace(authSubject) ||
            string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new ContractorSession(
            tenantId.Value,
            authSubject.Trim(),
            email.Trim().ToLowerInvariant());
    }

    private static async Task<Timesheet?> LoadTimesheetAsync(
        AppDbContext db,
        Guid tenantId,
        string authSubject,
        DateOnly weekStartUtc,
        CancellationToken cancellationToken) =>
        await db.Timesheets
            .Include(x => x.Entries)
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId &&
                     x.ContractorAuthSubject == authSubject &&
                     x.WeekStartUtc == weekStartUtc,
                cancellationToken);

    private static ProblemHttpResult? ApplyEntries(
        Timesheet timesheet,
        IReadOnlyCollection<ParsedTimesheetEntry> entries)
    {
        try
        {
            var existingKeys = timesheet.Entries
                .Select(x => new EntryKey(x.WorkDate, x.EntryType))
                .ToHashSet();

            var incomingKeys = entries
                .Select(x => new EntryKey(x.WorkDate, x.EntryType))
                .ToHashSet();

            foreach (var staleKey in existingKeys.Except(incomingKeys))
            {
                timesheet.RemoveEntry(staleKey.WorkDate, staleKey.EntryType);
            }

            foreach (var entry in entries)
            {
                timesheet.AddOrUpdateEntry(
                    entry.WorkDate,
                    entry.Hours,
                    entry.EntryType,
                    entry.Notes);
            }

            return null;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return TypedResults.Problem(
                title: "Invalid entry hours",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                title: "Timesheet update blocked",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static ParsedEntryBatch ParseEntries(UpsertContractorTimesheetRequest request)
    {
        var entries = new List<ParsedTimesheetEntry>(request.Entries.Length);
        var seenKeys = new HashSet<EntryKey>();

        foreach (var entry in request.Entries)
        {
            if (!Enum.TryParse<TimeEntryType>(entry.EntryType, ignoreCase: true, out var entryType))
            {
                return new ParsedEntryBatch([], $"Unsupported entry type '{entry.EntryType}'. Use Work or PaidTimeOff.");
            }

            var key = new EntryKey(entry.WorkDate, entryType);
            if (!seenKeys.Add(key))
            {
                return new ParsedEntryBatch([], $"Duplicate entry for {entry.WorkDate:yyyy-MM-dd} and {entry.EntryType}.");
            }

            entries.Add(new ParsedTimesheetEntry(
                entry.WorkDate,
                entry.Hours,
                entryType,
                Normalize(entry.Notes)));
        }

        return new ParsedEntryBatch(entries, null);
    }

    private static ContractorTimesheetResponse EmptyResponse(ContractorSession session, DateOnly weekStartUtc) =>
        new(
            null,
            session.TenantId,
            session.AuthSubject,
            session.Email,
            weekStartUtc,
            TimesheetStatus.Draft.ToString(),
            null,
            null,
            null,
            null,
            0,
            new TimesheetTotalsResponse(0, 0, 0, 0, 0),
            []);

    private static ContractorTimesheetResponse ToResponse(Timesheet timesheet)
    {
        var totals = timesheet.CalculateTotals();

        return new(
            timesheet.Id,
            timesheet.TenantId,
            timesheet.ContractorAuthSubject,
            timesheet.ContractorEmail,
            timesheet.WeekStartUtc,
            timesheet.Status.ToString(),
            timesheet.ReviewedByAuthSubject,
            timesheet.ReviewNote,
            timesheet.SubmittedAtUtc,
            timesheet.ReviewedAtUtc,
            timesheet.Entries.Sum(x => x.Hours),
            new TimesheetTotalsResponse(
                totals.WorkHours,
                totals.PaidTimeOffHours,
                totals.RegularHours,
                totals.OvertimeHours,
                totals.PayableHours),
            timesheet.Entries
                .OrderBy(x => x.WorkDate)
                .ThenBy(x => x.EntryType)
                .Select(x => new ContractorTimesheetEntryResponse(
                    x.WorkDate,
                    x.Hours,
                    x.EntryType.ToString(),
                    x.Notes))
                .ToArray());
    }

    private static ProblemHttpResult TenantRequired() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            detail: "Contractor time entry requires a tenant_id claim in the authenticated session.",
            statusCode: StatusCodes.Status412PreconditionFailed);

    private static DateOnly NormalizeWeekStart(DateOnly value)
    {
        var offset = ((int)value.DayOfWeek + 6) % 7;
        return value.AddDays(-offset);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ContractorSession(Guid TenantId, string AuthSubject, string Email);
    private sealed record EntryKey(DateOnly WorkDate, TimeEntryType EntryType);
    private sealed record ParsedTimesheetEntry(DateOnly WorkDate, decimal Hours, TimeEntryType EntryType, string? Notes);
    private sealed record ParsedEntryBatch(IReadOnlyCollection<ParsedTimesheetEntry> Entries, string? Error);
}

public sealed record ContractorTimesheetListResponse(
    ContractorTimesheetSummaryResponse[] Items);

public sealed record ContractorTimesheetSummaryResponse(
    Guid Id,
    DateOnly WeekStartUtc,
    string Status,
    decimal TotalHours,
    decimal PayableHours,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc);

public sealed record UpsertContractorTimesheetRequest(
    DateOnly WeekStartUtc,
    ContractorTimesheetEntryRequest[] Entries);

public sealed record ContractorTimesheetEntryRequest(
    DateOnly WorkDate,
    decimal Hours,
    string EntryType,
    string? Notes);

public sealed record ContractorTimesheetResponse(
    Guid? Id,
    Guid TenantId,
    string ContractorAuthSubject,
    string ContractorEmail,
    DateOnly WeekStartUtc,
    string Status,
    string? ReviewedByAuthSubject,
    string? ReviewNote,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    decimal TotalHours,
    TimesheetTotalsResponse Totals,
    ContractorTimesheetEntryResponse[] Entries);

public sealed record ContractorTimesheetEntryResponse(
    DateOnly WorkDate,
    decimal Hours,
    string EntryType,
    string? Notes);

public sealed record TimesheetTotalsResponse(
    decimal WorkHours,
    decimal PaidTimeOffHours,
    decimal RegularHours,
    decimal OvertimeHours,
    decimal PayableHours);
