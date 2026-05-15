using FluentAssertions;
using QuantamAnalytics.Infrastructure.AI;

namespace QuantamAnalytics.Tests;

public sealed class StubCopilotProviderTests
{
    [Fact]
    public async Task SummarizeCandidate_falls_back_when_fields_blank()
    {
        var copilot = new StubCopilotProvider();
        var result = await copilot.SummarizeCandidateAsync(
            new SummarizeCandidateRequest("", "", "", Array.Empty<string>()),
            default);

        result.Summary.Should().Contain("candidate");
        result.Highlights.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SummarizeCandidate_mentions_top_three_skills()
    {
        var copilot = new StubCopilotProvider();
        var result = await copilot.SummarizeCandidateAsync(
            new SummarizeCandidateRequest(
                "Avery", "Senior engineer", "Builds APIs.",
                ["Python", "Postgres", "React", "Kafka"]),
            default);

        result.Summary.Should().Contain("Python");
        result.Summary.Should().Contain("Postgres");
        result.Summary.Should().Contain("React");
        result.Summary.Should().NotContain("Kafka");
    }

    [Fact]
    public async Task ProposeInterviewQuestions_clamps_count_between_3_and_12()
    {
        var copilot = new StubCopilotProvider();

        var below = await copilot.ProposeInterviewQuestionsAsync(
            new ProposeInterviewQuestionsRequest("Senior Engineer", "Build APIs", Count: 0),
            default);
        below.Questions.Count.Should().Be(5); // default

        var ceil = await copilot.ProposeInterviewQuestionsAsync(
            new ProposeInterviewQuestionsRequest("Senior Engineer", "Build APIs", Count: 99),
            default);
        ceil.Questions.Count.Should().Be(12);
    }

    [Fact]
    public async Task Onboarding_checklist_includes_pulse_check_and_review()
    {
        var copilot = new StubCopilotProvider();

        var checklist = await copilot.ProposeOnboardingChecklistAsync(
            new ProposeOnboardingChecklistRequest("Senior Engineer", "Day 1"),
            default);

        checklist.Items.Should().Contain(x => x.Contains("30-day", StringComparison.OrdinalIgnoreCase));
        checklist.Items.Should().Contain(x => x.Contains("90-day", StringComparison.OrdinalIgnoreCase));
    }
}
