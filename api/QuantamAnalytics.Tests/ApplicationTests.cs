using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public void Ctor_sets_initial_applied_state()
    {
        var application = new Application(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "candidate@example.com",
            "Candidate One",
            "Interested in the role.");

        application.Id.Should().NotBe(Guid.Empty);
        application.Id.Version.Should().Be(7);
        application.CandidateEmail.Should().Be("candidate@example.com");
        application.CandidateName.Should().Be("Candidate One");
        application.Note.Should().Be("Interested in the role.");
        application.Status.Should().Be(ApplicationStatus.Applied);
        application.AppliedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Transition_methods_move_forward_and_stop_after_terminal_state()
    {
        var application = new Application(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "candidate@example.com",
            "Candidate One",
            null);

        application.TransitionToInterviewing();
        application.TransitionToOfferSent();
        application.TransitionToHired();

        application.Status.Should().Be(ApplicationStatus.Hired);

        var act = application.TransitionToRejected;
        act.Should().Throw<InvalidOperationException>();
    }
}
