using FluentAssertions;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class InterviewEventTests
{
    [Fact]
    public void Constructor_sets_scheduled_state_and_normalizes_fields()
    {
        var start = new DateTimeOffset(2026, 5, 20, 15, 0, 0, TimeSpan.Zero);
        var interview = new InterviewEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            " Jane Candidate ",
            " JANE@EXAMPLE.COM ",
            " Technical screen ",
            " Hiring Manager ",
            InterviewCalendarProvider.GoogleCalendar,
            start,
            start.AddHours(1));

        interview.Status.Should().Be(InterviewEventStatus.Scheduled);
        interview.CandidateName.Should().Be("Jane Candidate");
        interview.CandidateEmail.Should().Be("jane@example.com");
        interview.Title.Should().Be("Technical screen");
        interview.InterviewerName.Should().Be("Hiring Manager");
        interview.Provider.Should().Be(InterviewCalendarProvider.GoogleCalendar);
    }

    [Fact]
    public void Cancel_marks_event_cancelled()
    {
        var start = new DateTimeOffset(2026, 5, 20, 15, 0, 0, TimeSpan.Zero);
        var interview = new InterviewEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Jane Candidate",
            "jane@example.com",
            "Technical screen",
            "Hiring Manager",
            InterviewCalendarProvider.OutlookCalendar,
            start,
            start.AddHours(1));

        interview.Cancel();

        interview.Status.Should().Be(InterviewEventStatus.Cancelled);
        interview.CancelledAtUtc.Should().NotBeNull();
    }
}
