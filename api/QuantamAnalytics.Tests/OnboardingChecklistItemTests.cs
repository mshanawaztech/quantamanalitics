using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class OnboardingChecklistItemTests
{
    [Fact]
    public void Ctor_starts_in_Pending()
    {
        var item = NewItem();

        item.Status.Should().Be(OnboardingItemStatus.Pending);
        item.SubmittedAtUtc.Should().BeNull();
        item.ReviewedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Submit_sets_status_and_optional_note()
    {
        var item = NewItem();

        item.Submit("uploaded via HR portal");

        item.Status.Should().Be(OnboardingItemStatus.Submitted);
        item.CandidateNote.Should().Be("uploaded via HR portal");
        item.SubmittedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Submit_clears_prior_review_state()
    {
        var item = NewItem();
        item.Submit(null);
        item.Reject("blurry photo");

        item.Submit("re-uploaded clearer scan");

        item.Status.Should().Be(OnboardingItemStatus.Submitted);
        item.CandidateNote.Should().Be("re-uploaded clearer scan");
        item.ReviewerNote.Should().BeNull();
        item.ReviewedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Submit_throws_after_approved()
    {
        var item = NewItem();
        item.Submit(null);
        item.Approve("looks good");

        var act = () => item.Submit("trying again");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_requires_a_submitted_item()
    {
        var item = NewItem();

        var act = () => item.Approve(null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reject_requires_a_note_and_returns_to_pending()
    {
        var item = NewItem();
        item.Submit(null);

        item.Reject("missing signature");

        item.Status.Should().Be(OnboardingItemStatus.Pending);
        item.ReviewerNote.Should().Be("missing signature");
        item.SubmittedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Reject_without_note_throws()
    {
        var item = NewItem();
        item.Submit(null);

        var act = () => item.Reject(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reject_requires_a_submitted_item()
    {
        var item = NewItem();

        var act = () => item.Reject("note");

        act.Should().Throw<InvalidOperationException>();
    }

    private static OnboardingChecklistItem NewItem() => new(
        tenantId: Guid.CreateVersion7(),
        candidateProfileId: Guid.CreateVersion7(),
        assignedByAuthSubject: "auth0|recruiter",
        itemType: OnboardingItemType.W4,
        title: "Submit your W-4",
        instructions: "Use the latest IRS revision and sign on page 2.");
}
