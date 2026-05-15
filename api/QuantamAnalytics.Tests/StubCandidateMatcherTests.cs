using FluentAssertions;
using QuantamAnalytics.Infrastructure.AI;

namespace QuantamAnalytics.Tests;

public sealed class StubCandidateMatcherTests
{
    [Fact]
    public async Task Strong_match_when_candidate_skills_cover_the_jd()
    {
        var matcher = new StubCandidateMatcher();

        var score = await matcher.ScoreAsync(
            new CandidateMatchRequest(
                CandidateHeadline: "Senior software engineer",
                CandidateSummary: "Builds platforms.",
                CandidateSkills: ["Python", "AWS", "React"],
                JobTitle: "Senior Software Engineer",
                JobDescription: "Looking for someone fluent in Python, AWS, and React."),
            CancellationToken.None);

        score.Overall.Should().BeGreaterThan(0.6);
        score.SkillsCoverage.Should().BeGreaterThan(0.5);
        score.MatchedSkills.Should().Contain(["python", "aws", "react"]);
        score.Summary.Should().Contain("Strong match").Or.Contain("Promising fit");
    }

    [Fact]
    public async Task Likely_mismatch_when_no_skill_overlap_and_seniority_gap()
    {
        var matcher = new StubCandidateMatcher();

        var score = await matcher.ScoreAsync(
            new CandidateMatchRequest(
                CandidateHeadline: "Marketing analyst",
                CandidateSummary: "Spreadsheet and SEO work.",
                CandidateSkills: ["Excel", "SEO"],
                JobTitle: "Staff Backend Engineer",
                JobDescription: "Distributed systems leadership; PostgreSQL, Kubernetes, Go."),
            CancellationToken.None);

        score.Overall.Should().BeLessThan(0.5);
        score.SeniorityFit.Should().BeLessThan(0.6);
    }

    [Fact]
    public async Task Output_is_deterministic_for_same_inputs()
    {
        var matcher = new StubCandidateMatcher();
        var request = new CandidateMatchRequest(
            CandidateHeadline: "Senior engineer",
            CandidateSummary: "Builds APIs.",
            CandidateSkills: ["Python"],
            JobTitle: "Senior Engineer",
            JobDescription: "Python and Postgres.");

        var a = await matcher.ScoreAsync(request, default);
        var b = await matcher.ScoreAsync(request, default);

        a.Should().BeEquivalentTo(b);
    }
}
