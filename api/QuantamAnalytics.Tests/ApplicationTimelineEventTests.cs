using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class ApplicationTimelineEventTests
{
    [Fact]
    public void Ctor_assigns_identity_and_normalizes_optional_fields()
    {
        var occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-15);

        var @event = new ApplicationTimelineEvent(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ApplicationTimelineEventType.NoteAdded,
            ApplicationTimelineAudience.RecruiterOnly,
            "  Internal recruiter note ",
            "  Candidate requested a late interview slot. ",
            "  Recruiter Ops  ",
            occurredAtUtc);

        @event.Id.Should().NotBe(Guid.Empty);
        @event.Id.Version.Should().Be(7);
        @event.Title.Should().Be("Internal recruiter note");
        @event.Description.Should().Be("Candidate requested a late interview slot.");
        @event.ActorLabel.Should().Be("Recruiter Ops");
        @event.OccurredAtUtc.Should().Be(occurredAtUtc);
    }
}
