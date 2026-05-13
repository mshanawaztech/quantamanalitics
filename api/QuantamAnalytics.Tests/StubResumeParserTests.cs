using System.Text;
using FluentAssertions;
using QuantamAnalytics.Infrastructure.ResumeParsing;

namespace QuantamAnalytics.Tests;

/// <summary>
/// The stub resume parser is the implementation recruiters will hit until a
/// real vendor lands — its determinism is the whole point. These tests
/// pin that contract so a casual edit to the seed pools can't silently
/// break the demo data flow.
/// </summary>
public sealed class StubResumeParserTests
{
    [Fact]
    public async Task ParseAsync_returns_extracted_email_when_present_in_text()
    {
        var parser = new StubResumeParser();
        var snippet = "Hello, I am Sam and you can reach me at sam.engineer@example.com or call.";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(snippet));

        var result = await parser.ParseAsync(
            new ResumeParseRequest("sam-resume.txt", "text/plain", stream),
            CancellationToken.None);

        result.Email.Should().Be("sam.engineer@example.com");
    }

    [Fact]
    public async Task ParseAsync_falls_back_to_generated_email_when_none_in_text()
    {
        var parser = new StubResumeParser();
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("opaque binary content"));

        var result = await parser.ParseAsync(
            new ResumeParseRequest("portfolio-binary.pdf", "application/pdf", stream),
            CancellationToken.None);

        result.Email.Should().NotBeNullOrWhiteSpace();
        result.Email.Should().EndWith("@example.com");
    }

    [Fact]
    public async Task ParseAsync_is_deterministic_for_a_given_file_name()
    {
        var parser = new StubResumeParser();
        using var first = new MemoryStream(Encoding.ASCII.GetBytes(""));
        using var second = new MemoryStream(Encoding.ASCII.GetBytes(""));

        var a = await parser.ParseAsync(new ResumeParseRequest("alice.pdf", "application/pdf", first), default);
        var b = await parser.ParseAsync(new ResumeParseRequest("alice.pdf", "application/pdf", second), default);

        a.FullName.Should().Be(b.FullName);
        a.Headline.Should().Be(b.Headline);
        a.Skills.Should().BeEquivalentTo(b.Skills, opt => opt.WithStrictOrdering());
        a.WorkHistory.Should().BeEquivalentTo(b.WorkHistory, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public async Task ParseAsync_returns_at_least_two_work_history_entries()
    {
        var parser = new StubResumeParser();
        using var stream = new MemoryStream();

        var result = await parser.ParseAsync(
            new ResumeParseRequest("any.pdf", "application/pdf", stream),
            CancellationToken.None);

        result.WorkHistory.Length.Should().BeGreaterOrEqualTo(2);

        // The most recent job (index 0) is the current role — must have no end date.
        result.WorkHistory[0].EndDate.Should().BeNull();

        // Every prior entry has both endpoints set.
        foreach (var prior in result.WorkHistory.Skip(1))
        {
            prior.StartDate.Should().NotBeNull();
            prior.EndDate.Should().NotBeNull();
            (prior.StartDate!.Value < prior.EndDate!.Value).Should().BeTrue(
                "prior work history entries have start before end");
        }
    }

    [Fact]
    public async Task ParseAsync_returns_six_unique_skills()
    {
        var parser = new StubResumeParser();
        using var stream = new MemoryStream();

        var result = await parser.ParseAsync(
            new ResumeParseRequest("skill-test.pdf", "application/pdf", stream),
            CancellationToken.None);

        result.Skills.Should().HaveCount(6);
        result.Skills.Distinct().Should().HaveCount(6);
    }
}
