using System.Text.RegularExpressions;

namespace QuantamAnalytics.Infrastructure.AI;

/// <summary>
/// Deterministic candidate-job matcher. Computes a real (if simple) signal:
///
///   - SkillsCoverage: fraction of job-description tokens that appear in
///     the candidate's declared skill list.
///   - SeniorityFit: matches "senior" / "lead" / "principal" / "staff"
///     keywords across both sides; 1.0 when they agree, 0.6 when one is
///     senior and the other is silent, 0.3 when they conflict.
///   - Overall: weighted blend (0.7 SkillsCoverage + 0.3 SeniorityFit),
///     clamped to [0, 1].
///
/// Output is stable — the same inputs always produce the same score —
/// so recruiter walkthroughs don't drift between demos. The real
/// implementation lands later; this gets the UI loop closed today.
/// </summary>
public sealed partial class StubCandidateMatcher : ICandidateMatcher
{
    private static readonly Regex TokenRegex = MyTokenRegex();

    private static readonly string[] SeniorityWords =
        ["senior", "lead", "principal", "staff", "architect"];

    public Task<CandidateMatchScore> ScoreAsync(
        CandidateMatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidateSkills = request.CandidateSkills
            .Select(NormalizeToken)
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var jobTokens = TokenRegex.Matches(request.JobDescription)
            .Select(m => NormalizeToken(m.Value))
            .Where(s => s.Length > 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Matched skills = candidate skills that show up in the JD body.
        // Gap skills = JD tokens that look like skills but aren't on the
        // candidate. We can't know which JD tokens are "skills" without
        // an NLP pass, so we constrain to ones longer than 4 chars that
        // appear in title-case or all-caps — a crude but stable proxy.
        var matched = candidateSkills
            .Where(s => jobTokens.Contains(s))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        var probableJdSkills = ExtractProbableSkills(
            request.JobTitle + " " + request.JobDescription);
        var gaps = probableJdSkills
            .Where(s => !candidateSkills.Contains(s))
            .OrderBy(s => s, StringComparer.Ordinal)
            .Take(6)
            .ToArray();

        var coverage = probableJdSkills.Count == 0
            ? 0.0
            : (double)matched.Length / probableJdSkills.Count;
        coverage = Math.Clamp(coverage, 0.0, 1.0);

        var seniority = ComputeSeniorityFit(request);

        var overall = Math.Clamp(0.7 * coverage + 0.3 * seniority, 0.0, 1.0);

        var summary = BuildSummary(overall, matched, gaps);

        return Task.FromResult(new CandidateMatchScore(
            Overall: Math.Round(overall, 2),
            SkillsCoverage: Math.Round(coverage, 2),
            SeniorityFit: Math.Round(seniority, 2),
            MatchedSkills: matched,
            GapSkills: gaps,
            Summary: summary));
    }

    private static double ComputeSeniorityFit(CandidateMatchRequest request)
    {
        var candidateSenior = MentionsSeniority(request.CandidateHeadline + " " + request.CandidateSummary);
        var jobSenior = MentionsSeniority(request.JobTitle + " " + request.JobDescription);

        return (candidateSenior, jobSenior) switch
        {
            (true, true) => 1.0,
            (false, false) => 1.0,
            (true, false) => 0.8,   // overqualified-looking; still a fit
            (false, true) => 0.4,   // junior on a senior listing — real gap
        };
    }

    private static bool MentionsSeniority(string text)
    {
        var lower = text.ToLowerInvariant();
        return SeniorityWords.Any(w => lower.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> ExtractProbableSkills(string text)
    {
        var matches = TokenRegex.Matches(text);
        return matches
            .Select(m => m.Value)
            .Where(IsLikelySkill)
            .Select(NormalizeToken)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsLikelySkill(string token)
    {
        if (token.Length <= 2) return false;
        // Title-cased ("Python", "React") or all-caps ("AWS", "SQL").
        return char.IsUpper(token[0]) || token.All(char.IsUpper);
    }

    private static string NormalizeToken(string raw) =>
        raw.Trim().Trim(',', '.', ';', ':', '(', ')', '[', ']').ToLowerInvariant();

    // CA1859: private helper is only called with arrays — use the concrete
    // type so the JIT can skip the interface dispatch.
    private static string BuildSummary(
        double overall,
        string[] matched,
        string[] gaps)
    {
        var label = overall switch
        {
            >= 0.8 => "Strong match",
            >= 0.6 => "Promising fit",
            >= 0.4 => "Worth a screen",
            _ => "Likely mismatch",
        };

        var matchedPart = matched.Length > 0
            ? $"Aligned on: {string.Join(", ", matched.Take(4))}."
            : "No declared skills overlap with the job description.";

        var gapsPart = gaps.Length > 0
            ? $" Likely gaps: {string.Join(", ", gaps.Take(4))}."
            : string.Empty;

        return $"{label}. {matchedPart}{gapsPart}";
    }

    [GeneratedRegex(@"[\w\.+#-]+", RegexOptions.CultureInvariant)]
    private static partial Regex MyTokenRegex();
}
