using FluentAssertions;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class OfferLetterTests
{
    [Fact]
    public void New_offer_is_drafted()
    {
        var offer = New();

        offer.Status.Should().Be(OfferLetterStatus.Drafted);
        offer.SentAtUtc.Should().BeNull();
        offer.ResolvedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Send_only_from_drafted()
    {
        var offer = New();

        offer.Send();
        offer.Status.Should().Be(OfferLetterStatus.Sent);

        var act = () => offer.Send();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Accept_only_from_sent_and_records_note()
    {
        var offer = New();
        offer.Send();

        offer.RecordAcceptance("Looking forward to starting.");

        offer.Status.Should().Be(OfferLetterStatus.Accepted);
        offer.ResolvedAtUtc.Should().NotBeNull();
        offer.CandidateResponseNote.Should().Be("Looking forward to starting.");
    }

    [Fact]
    public void Decline_only_from_sent()
    {
        var offer = New();

        var act = () => offer.RecordDecline(null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Withdraw_blocked_after_resolution()
    {
        var offer = New();
        offer.Send();
        offer.RecordAcceptance(null);

        var act = () => offer.Withdraw();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been resolved*");
    }

    [Fact]
    public void UpdateDraft_rejects_negative_salary()
    {
        var offer = New();
        var act = () => offer.UpdateDraft("body", -1m, DateOnly.FromDateTime(DateTime.UtcNow));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static OfferLetter New() =>
        new(
            tenantId: Guid.CreateVersion7(),
            candidateProfileId: Guid.CreateVersion7(),
            applicationId: null,
            title: "Senior Engineer Offer",
            baseAnnualSalary: 140_000m,
            currency: "usd",
            startDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(30)),
            bodyMarkdown: "# Offer\nWelcome!",
            createdByAuthSubject: "auth0|rec");
}
