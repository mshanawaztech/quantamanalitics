using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Interviews;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

public static class InterviewSchedulingEndpoint
{
    public static IEndpointRouteBuilder MapInterviewSchedulingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/interviews")
            .WithTags("Interview Scheduling")
            .RequireAuthorization(AuthorizationPolicies.RequireInterviewAccess);

        group.MapGet("/overview", GetOverviewAsync);
        group.MapPost("/{id:guid}/meeting-link", GenerateMeetingLinkAsync);

        return app;
    }

    private static async Task<Results<Ok<InterviewMeetingLinkResponse>, NotFound, ProblemHttpResult>> GenerateMeetingLinkAsync(
        Guid id,
        AppDbContext db,
        ICurrentTenant currentTenant,
        IMeetingLinkGenerator linkGenerator,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Generating an interview meeting link requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        // Tenant filter on AppDbContext makes this lookup naturally tenant-safe:
        // recruiters in tenant A can't mint links for events owned by tenant B.
        var interview = await db.InterviewEvents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (interview is null)
        {
            return TypedResults.NotFound();
        }

        var url = linkGenerator.Generate(interview.Provider, interview.Id);
        interview.AttachMeetingJoinUrl(url);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new InterviewMeetingLinkResponse(
            interview.Id,
            interview.Provider.ToString(),
            url,
            interview.UpdatedAtUtc));
    }

    private static async Task<Results<Ok<InterviewOverviewResponse>, ProblemHttpResult>> GetOverviewAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        IInterviewCalendarProviderCatalog providerCatalog,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Interview planning requires a tenant_id claim in the authenticated session.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var events = await db.InterviewEvents
            .OrderBy(x => x.ScheduledStartUtc)
            .Select(x => new InterviewOverviewEventResponse(
                x.Id,
                x.SubmissionId,
                x.CandidateName,
                x.CandidateEmail,
                x.Title,
                x.InterviewerName,
                x.Provider.ToString(),
                x.Status.ToString(),
                x.ScheduledStartUtc,
                x.ScheduledEndUtc,
                x.MeetingJoinUrl,
                x.ExternalEventId))
            .ToArrayAsync(cancellationToken);

        var providers = providerCatalog.GetProviders()
            .Select(x => new InterviewOverviewProviderResponse(x.Name, x.Status, x.Detail))
            .ToArray();

        return TypedResults.Ok(new InterviewOverviewResponse(providers, events));
    }
}

public sealed record InterviewOverviewResponse(
    InterviewOverviewProviderResponse[] Providers,
    InterviewOverviewEventResponse[] Events);

public sealed record InterviewOverviewProviderResponse(
    string Name,
    string Status,
    string Detail);

public sealed record InterviewOverviewEventResponse(
    Guid Id,
    Guid SubmissionId,
    string CandidateName,
    string CandidateEmail,
    string Title,
    string InterviewerName,
    string Provider,
    string Status,
    DateTimeOffset ScheduledStartUtc,
    DateTimeOffset ScheduledEndUtc,
    string? MeetingJoinUrl,
    string? ExternalEventId);

/// <summary>
/// Returned when a recruiter mints (or refreshes) a meeting join URL for an
/// interview event. The URL is deterministic in Phase 3 — re-calling the
/// endpoint yields the same value — so this response is safe to cache on the
/// SPA side until the interview is rescheduled or cancelled.
/// </summary>
public sealed record InterviewMeetingLinkResponse(
    Guid InterviewEventId,
    string Provider,
    string MeetingJoinUrl,
    DateTimeOffset UpdatedAtUtc);
