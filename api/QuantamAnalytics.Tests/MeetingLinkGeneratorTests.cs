using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Interviews;

namespace QuantamAnalytics.Tests;

/// <summary>
/// Pure unit tests for the Phase 3 stub generator. The contract is:
///   - same inputs always produce the same URL (deterministic)
///   - the URL shape includes a per-provider segment so a future split into
///     ZoomGenerator / TeamsGenerator is a drop-in replacement
///   - empty interview ids are rejected loudly
/// </summary>
public sealed class MeetingLinkGeneratorTests
{
    private readonly MeetingLinkGenerator _generator = new();

    [Fact]
    public void Generate_is_deterministic_for_the_same_inputs()
    {
        var id = Guid.CreateVersion7();

        var first = _generator.Generate(InterviewCalendarProvider.GoogleCalendar, id);
        var second = _generator.Generate(InterviewCalendarProvider.GoogleCalendar, id);

        first.Should().Be(second);
    }

    [Fact]
    public void Generate_uses_google_segment_for_google_calendar()
    {
        var url = _generator.Generate(InterviewCalendarProvider.GoogleCalendar, Guid.CreateVersion7());

        url.Should().StartWith("https://meet.scaffold.quantamanalitics.com/google/");
    }

    [Fact]
    public void Generate_uses_teams_segment_for_outlook_calendar()
    {
        var url = _generator.Generate(InterviewCalendarProvider.OutlookCalendar, Guid.CreateVersion7());

        url.Should().StartWith("https://meet.scaffold.quantamanalitics.com/teams/");
    }

    [Fact]
    public void Generate_throws_on_empty_event_id()
    {
        var act = () => _generator.Generate(InterviewCalendarProvider.GoogleCalendar, Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_uses_compact_lowercase_event_id_in_the_path()
    {
        var id = new Guid("11111111-2222-3333-4444-555555555555");

        var url = _generator.Generate(InterviewCalendarProvider.GoogleCalendar, id);

        url.Should().EndWith("/11111111222233334444555555555555");
    }
}
