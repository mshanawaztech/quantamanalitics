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
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        group.MapGet("/overview", GetOverviewAsync);

        return app;
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
