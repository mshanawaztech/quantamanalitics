using FluentAssertions;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class TenantSubscriptionTests
{
    [Fact]
    public void New_subscription_is_trialing_for_14_days()
    {
        var sub = New();
        sub.Status.Should().Be(TenantSubscriptionStatus.Trialing);
        sub.TrialEndsAtUtc.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddDays(14), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Activate_clears_trial_and_records_stripe_id()
    {
        var sub = New();
        var periodEnd = DateTimeOffset.UtcNow.AddDays(30);

        sub.Activate("sub_abc123", periodEnd);

        sub.Status.Should().Be(TenantSubscriptionStatus.Active);
        sub.StripeSubscriptionId.Should().Be("sub_abc123");
        sub.CurrentPeriodEndsAtUtc.Should().Be(periodEnd);
        sub.TrialEndsAtUtc.Should().BeNull();
    }

    [Fact]
    public void PastDue_only_from_Active()
    {
        var sub = New();
        var act = () => sub.MarkPastDue();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reactivate_only_from_PastDue()
    {
        var sub = New();
        sub.Activate("sub_a", DateTimeOffset.UtcNow.AddDays(30));
        sub.MarkPastDue();
        sub.Reactivate(DateTimeOffset.UtcNow.AddDays(30));
        sub.Status.Should().Be(TenantSubscriptionStatus.Active);
    }

    [Fact]
    public void Cancel_is_idempotent()
    {
        var sub = New();
        sub.Cancel();
        sub.Cancel();
        sub.Status.Should().Be(TenantSubscriptionStatus.Cancelled);
    }

    [Fact]
    public void IsEntitled_matches_status_table()
    {
        var sub = New();
        var now = DateTimeOffset.UtcNow;

        // Inside trial → entitled.
        sub.IsEntitled(now).Should().BeTrue();

        // Past trial window → not entitled.
        sub.IsEntitled(now.AddDays(30)).Should().BeFalse();

        // Active → entitled regardless of when we ask.
        sub.Activate("sub_a", now.AddDays(30));
        sub.IsEntitled(now).Should().BeTrue();
        sub.IsEntitled(now.AddDays(60)).Should().BeTrue();

        // PastDue → still entitled (grace period).
        sub.MarkPastDue();
        sub.IsEntitled(now).Should().BeTrue();

        // Cancelled → locked out.
        sub.Cancel();
        sub.IsEntitled(now).Should().BeFalse();
    }

    [Fact]
    public void ChangeSeatCount_rejects_zero()
    {
        var sub = New();
        var act = () => sub.ChangeSeatCount(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static TenantSubscription New() =>
        new(
            tenantId: Guid.CreateVersion7(),
            planCode: "team",
            includedSeats: 5,
            pricePerSeat: 49m,
            currency: "usd");
}
