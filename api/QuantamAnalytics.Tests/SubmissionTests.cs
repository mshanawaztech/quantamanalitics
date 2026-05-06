using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class SubmissionTests
{
    [Fact]
    public void Ctor_sets_initial_draft_state()
    {
        var submission = new Submission(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "candidate@example.com",
            "Candidate One");

        submission.Id.Should().NotBe(Guid.Empty);
        submission.Id.Version.Should().Be(7);
        submission.CandidateEmail.Should().Be("candidate@example.com");
        submission.CandidateName.Should().Be("Candidate One");
        submission.Status.Should().Be(SubmissionStatus.Draft);
        submission.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Submit_to_client_sets_handoff_state()
    {
        var submission = NewSubmission();

        submission.SubmitToClient(
            "auth0|recruiter-1",
            "Acme Client",
            "High-signal candidate for review.");

        submission.Status.Should().Be(SubmissionStatus.SubmittedToClient);
        submission.SubmittedByAuthSubject.Should().Be("auth0|recruiter-1");
        submission.ClientCompanyName.Should().Be("Acme Client");
        submission.PitchSummary.Should().Be("High-signal candidate for review.");
        submission.SubmittedToClientAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Reviewing_accept_and_decline_follow_valid_state_changes()
    {
        var accepted = NewSubmittedSubmission();
        accepted.MarkClientReviewing();
        accepted.AcceptByClient("Approved for next-round interview");

        accepted.Status.Should().Be(SubmissionStatus.ClientAccepted);
        accepted.ClientDecisionNote.Should().Be("Approved for next-round interview");

        var declined = NewSubmittedSubmission();
        declined.DeclineByClient("No longer aligned");
        declined.Status.Should().Be(SubmissionStatus.ClientDeclined);
        declined.ClientDecisionNote.Should().Be("No longer aligned");
    }

    [Fact]
    public void Withdraw_blocks_terminal_or_duplicate_cases()
    {
        var submission = NewSubmittedSubmission();
        submission.Withdraw("Candidate accepted another offer");

        submission.Status.Should().Be(SubmissionStatus.Withdrawn);

        var act = () => submission.Withdraw("Second attempt");
        act.Should().Throw<InvalidOperationException>();
    }

    private static Submission NewSubmission() =>
        new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "candidate@example.com",
            "Candidate One");

    private static Submission NewSubmittedSubmission()
    {
        var submission = NewSubmission();
        submission.SubmitToClient("auth0|recruiter-1", "Acme Client", null);
        return submission;
    }
}
