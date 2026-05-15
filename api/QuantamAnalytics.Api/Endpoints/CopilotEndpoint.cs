using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Infrastructure.AI;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Phase 9 / Story 73 — AI copilot wrappers. Three recruiter-facing
/// endpoints that read source records inside the tenant query filter
/// and hand them to ICopilotProvider for a structured suggestion.
/// </summary>
public static class CopilotEndpoint
{
    public static IEndpointRouteBuilder MapCopilotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recruiter/copilot")
            .WithTags("AI copilot")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        group.MapPost("/candidates/{candidateId:guid}/summary", SummarizeCandidateAsync);
        group.MapPost("/jobs/{jobId:guid}/interview-questions", InterviewQuestionsAsync);
        group.MapPost("/onboarding-checklist", OnboardingChecklistAsync);

        return app;
    }

    private static async Task<Results<Ok<CopilotCandidateSummary>, NotFound, ProblemHttpResult>> SummarizeCandidateAsync(
        Guid candidateId,
        AppDbContext db,
        ICopilotProvider copilot,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var candidate = await db.CandidateProfiles
            .Where(c => c.Id == candidateId)
            .Select(c => new
            {
                c.FullName,
                c.Headline,
                c.Summary,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate is null) return TypedResults.NotFound();

        var summary = await copilot.SummarizeCandidateAsync(
            new SummarizeCandidateRequest(
                CandidateName: candidate.FullName ?? string.Empty,
                Headline: candidate.Headline ?? string.Empty,
                Summary: candidate.Summary ?? string.Empty,
                Skills: Array.Empty<string>()),
            cancellationToken);

        return TypedResults.Ok(summary);
    }

    private static async Task<Results<Ok<CopilotInterviewQuestions>, NotFound, ProblemHttpResult>> InterviewQuestionsAsync(
        Guid jobId,
        InterviewQuestionsRequest? body,
        AppDbContext db,
        ICopilotProvider copilot,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return TenantRequired();

        var job = await db.Jobs
            .Where(j => j.Id == jobId)
            .Select(j => new { j.Title, j.Description })
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null) return TypedResults.NotFound();

        var questions = await copilot.ProposeInterviewQuestionsAsync(
            new ProposeInterviewQuestionsRequest(
                JobTitle: job.Title,
                JobDescription: job.Description,
                Count: body?.Count ?? 5),
            cancellationToken);

        return TypedResults.Ok(questions);
    }

    private static async Task<Ok<CopilotOnboardingChecklist>> OnboardingChecklistAsync(
        OnboardingChecklistRequest request,
        ICopilotProvider copilot,
        CancellationToken cancellationToken)
    {
        var result = await copilot.ProposeOnboardingChecklistAsync(
            new ProposeOnboardingChecklistRequest(
                RoleTitle: request.RoleTitle,
                StartDateLabel: request.StartDateLabel),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static ProblemHttpResult TenantRequired() =>
        TypedResults.Problem(
            title: "Tenant assignment required",
            statusCode: StatusCodes.Status412PreconditionFailed);
}

public sealed record InterviewQuestionsRequest(int? Count);
public sealed record OnboardingChecklistRequest(string RoleTitle, string StartDateLabel);
