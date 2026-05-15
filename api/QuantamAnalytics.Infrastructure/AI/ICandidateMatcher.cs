namespace QuantamAnalytics.Infrastructure.AI;

/// <summary>
/// Phase 9 / Story 67 boundary — scores a candidate against a job description
/// and produces a structured scorecard. The deterministic stub ships first
/// so the recruiter UX can be wired before any tenant pays for a real model
/// (OpenAI, Anthropic, internal SBERT, etc.). Same pattern as ICheckrClient
/// and IResumeParser.
/// </summary>
public interface ICandidateMatcher
{
    /// <summary>
    /// Score the (candidate, job) pair. Implementations must be tenant-blind
    /// — the caller is responsible for ensuring both records belong to the
    /// current tenant before invoking.
    /// </summary>
    Task<CandidateMatchScore> ScoreAsync(
        CandidateMatchRequest request,
        CancellationToken cancellationToken);
}

public sealed record CandidateMatchRequest(
    string CandidateHeadline,
    string CandidateSummary,
    IReadOnlyList<string> CandidateSkills,
    string JobTitle,
    string JobDescription);

public sealed record CandidateMatchScore(
    double Overall,
    double SkillsCoverage,
    double SeniorityFit,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> GapSkills,
    string Summary);
