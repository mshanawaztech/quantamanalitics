using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Infrastructure.AI;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Phase 9 / Story 67 — recruiter-facing scorecard for (candidate, job).
/// Reads both records inside the current-tenant query filter, hands them
/// to ICandidateMatcher, returns the scorecard. The boundary is
/// recruiter-only because matching reveals fitness signals that aren't
/// meant for the candidate themselves.
/// </summary>
public static class CandidateMatchEndpoint
{
    public static IEndpointRouteBuilder MapCandidateMatchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/v1/recruiter/candidates/{candidateId:guid}/match/{jobId:guid}",
                ScoreAsync)
            .WithTags("AI candidate matching")
            .RequireAuthorization(AuthorizationPolicies.RequireRecruitingAccess);

        return app;
    }

    private static async Task<Results<Ok<CandidateMatchResponse>, NotFound, ProblemHttpResult>> ScoreAsync(
        Guid candidateId,
        Guid jobId,
        AppDbContext db,
        ICandidateMatcher matcher,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Candidate matching requires a tenant_id claim.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        // Both lookups ride the global query filter so cross-tenant ids 404.
        var candidate = await db.CandidateProfiles
            .Where(c => c.Id == candidateId)
            .Select(c => new
            {
                c.Headline,
                c.Summary,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return TypedResults.NotFound();
        }

        var job = await db.Jobs
            .Where(j => j.Id == jobId)
            .Select(j => new { j.Title, j.Description })
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return TypedResults.NotFound();
        }

        // Skills aren't on CandidateProfile today; pull whatever the
        // candidate's resume parser surfaced. The matcher tolerates empty.
        var skills = Array.Empty<string>();

        var score = await matcher.ScoreAsync(
            new CandidateMatchRequest(
                CandidateHeadline: candidate.Headline ?? string.Empty,
                CandidateSummary: candidate.Summary ?? string.Empty,
                CandidateSkills: skills,
                JobTitle: job.Title,
                JobDescription: job.Description),
            cancellationToken);

        return TypedResults.Ok(new CandidateMatchResponse(
            score.Overall,
            score.SkillsCoverage,
            score.SeniorityFit,
            score.MatchedSkills,
            score.GapSkills,
            score.Summary));
    }
}

public sealed record CandidateMatchResponse(
    double Overall,
    double SkillsCoverage,
    double SeniorityFit,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> GapSkills,
    string Summary);
