using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Phase 9 / Story 68 — interviewer availability + candidate-facing
/// self-booking slot listing. The booking itself flows into the existing
/// InterviewScheduling endpoint shipped in Phase 3; this PR adds the
/// "what hours can I book?" half so candidates don't email back-and-
/// forth.
/// </summary>
public static class SchedulingEndpoint
{
    public static IEndpointRouteBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder app)
    {
        // Interviewer self-management.
        var interviewer = app.MapGroup("/api/v1/interviewer/availability")
            .WithTags("Interviewer availability")
            .RequireAuthorization(AuthorizationPolicies.RequireInterviewAccess);

        interviewer.MapGet("/", ListMyAvailabilityAsync);
        interviewer.MapPost("/", AddMyAvailabilityAsync);
        interviewer.MapDelete("/{id:guid}", RemoveMyAvailabilityAsync);

        // Candidate-facing slot picker — anonymous read because public job
        // applicants need to see availability before they authenticate.
        // The interviewer subject is required so we can't enumerate the
        // tenant's roster from one URL.
        app.MapGet(
                "/api/v1/scheduling/slots",
                ListSlotsAsync)
            .WithTags("Interviewer availability");

        return app;
    }

    private static async Task<Results<Ok<InterviewerWindowResponse[]>, ProblemHttpResult>> ListMyAvailabilityAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return SubjectRequired();
        }

        var subject = currentUser.AuthSubject;
        // Pull the raw TimeOnly + DayOfWeek values out of the query first;
        // format to "HH:mm" client-side so CA1305 (locale-sensitive ToString)
        // is satisfied, and so EF doesn't need to translate a format-string
        // call to SQL (which it can't).
        var raw = await db.InterviewerAvailability
            .Where(x => x.InterviewerAuthSubject == subject)
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTimeUtc)
            .Select(x => new { x.Id, x.DayOfWeek, x.StartTimeUtc, x.EndTimeUtc })
            .ToArrayAsync(cancellationToken);

        var rows = raw
            .Select(x => new InterviewerWindowResponse(
                x.Id,
                x.DayOfWeek.ToString(),
                x.StartTimeUtc.ToString("HH:mm", CultureInfo.InvariantCulture),
                x.EndTimeUtc.ToString("HH:mm", CultureInfo.InvariantCulture)))
            .ToArray();

        return TypedResults.Ok(rows);
    }

    private static async Task<Results<Created<InterviewerWindowResponse>, ProblemHttpResult>> AddMyAvailabilityAsync(
        AddInterviewerWindowRequest request,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return SubjectRequired();
        }

        if (!TimeOnly.TryParse(request.StartTimeUtc, out var start) ||
            !TimeOnly.TryParse(request.EndTimeUtc, out var end))
        {
            return TypedResults.Problem(
                title: "Invalid time range",
                detail: "StartTimeUtc and EndTimeUtc must be HH:mm strings.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        InterviewerAvailability window;
        try
        {
            window = new InterviewerAvailability(
                currentTenant.TenantId.Value,
                currentUser.AuthSubject,
                request.DayOfWeek,
                start,
                end);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(
                title: "Invalid availability window",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        db.InterviewerAvailability.Add(window);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v1/interviewer/availability/{window.Id}",
            new InterviewerWindowResponse(
                window.Id,
                window.DayOfWeek.ToString(),
                window.StartTimeUtc.ToString("HH:mm", CultureInfo.InvariantCulture),
                window.EndTimeUtc.ToString("HH:mm", CultureInfo.InvariantCulture)));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> RemoveMyAvailabilityAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return SubjectRequired();
        }

        var subject = currentUser.AuthSubject;
        var window = await db.InterviewerAvailability
            .FirstOrDefaultAsync(
                x => x.Id == id && x.InterviewerAuthSubject == subject,
                cancellationToken);
        if (window is null)
        {
            return TypedResults.NotFound();
        }

        db.InterviewerAvailability.Remove(window);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // CA1068: CancellationToken must be the last parameter, even after
    // optional string query parameters.
    private static async Task<Results<Ok<BookableSlotsResponse>, ProblemHttpResult>> ListSlotsAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        string interviewerAuthSubject,
        string? fromDateUtc = null,
        string? toDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        if (string.IsNullOrWhiteSpace(interviewerAuthSubject))
        {
            return TypedResults.Problem(
                title: "Interviewer required",
                detail: "Specify interviewerAuthSubject so we don't enumerate the roster.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var from = ParseDateOnly(fromDateUtc) ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var to = ParseDateOnly(toDateUtc) ?? from.AddDays(7);
        if (to <= from || (to.DayNumber - from.DayNumber) > 30)
        {
            return TypedResults.Problem(
                title: "Invalid date range",
                detail: "Range must be 1–30 days and end after start.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var windows = await db.InterviewerAvailability
            .Where(x => x.InterviewerAuthSubject == interviewerAuthSubject)
            .ToArrayAsync(cancellationToken);

        // Expand each recurring window across the requested date range,
        // chunked into 30-minute candidate-bookable slots. Existing
        // InterviewEvents on the same interviewer would normally subtract
        // from this list — left as a follow-up (the InterviewEvent table
        // tracks owners differently across portals).
        var slots = new List<SlotResponse>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var matching = windows.Where(w => w.DayOfWeek == day.DayOfWeek);
            foreach (var w in matching)
            {
                var cursor = w.StartTimeUtc;
                while (cursor.AddMinutes(30) <= w.EndTimeUtc)
                {
                    var startUtc = day.ToDateTime(cursor, DateTimeKind.Utc);
                    var endUtc = startUtc.AddMinutes(30);
                    slots.Add(new SlotResponse(
                        StartUtc: new DateTimeOffset(startUtc, TimeSpan.Zero),
                        EndUtc: new DateTimeOffset(endUtc, TimeSpan.Zero)));
                    cursor = cursor.AddMinutes(30);
                }
            }
        }

        return TypedResults.Ok(new BookableSlotsResponse(slots.ToArray(), slots.Count));
    }

    private static DateOnly? ParseDateOnly(string? raw) =>
        DateOnly.TryParse(raw, out var d) ? d : null;

    private static ProblemHttpResult SubjectRequired() =>
        TypedResults.Problem(
            title: "Authenticated tenant subject required",
            detail: "Interviewer availability needs a tenant_id claim and authenticated subject.",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record InterviewerWindowResponse(Guid Id, string DayOfWeek, string StartTimeUtc, string EndTimeUtc);

public sealed record AddInterviewerWindowRequest(DayOfWeek DayOfWeek, string StartTimeUtc, string EndTimeUtc);

public sealed record BookableSlotsResponse(SlotResponse[] Items, int Count);

public sealed record SlotResponse(DateTimeOffset StartUtc, DateTimeOffset EndUtc);
