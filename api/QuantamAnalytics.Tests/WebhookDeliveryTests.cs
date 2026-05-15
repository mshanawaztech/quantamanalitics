using FluentAssertions;
using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class WebhookDeliveryTests
{
    [Fact]
    public void Failure_schedules_exponential_backoff()
    {
        var d = NewDelivery();
        var now = DateTimeOffset.UtcNow;

        d.RecordFailure(502, "bad gateway", now);
        d.NextAttemptAtUtc.Should().BeCloseTo(now.AddMinutes(1), TimeSpan.FromSeconds(1));

        d.RecordFailure(502, null, now.AddMinutes(1));
        d.NextAttemptAtUtc.Should().BeCloseTo(now.AddMinutes(3), TimeSpan.FromSeconds(1));
        d.AttemptCount.Should().Be(2);
    }

    [Fact]
    public void After_eight_failures_status_is_Failed()
    {
        var d = NewDelivery();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 8; i++)
        {
            d.RecordFailure(500, null, now.AddMinutes(i));
        }
        d.Status.Should().Be(WebhookDeliveryStatus.Failed);
    }

    [Fact]
    public void Success_terminates_retry_loop()
    {
        var d = NewDelivery();
        d.RecordFailure(500, null, DateTimeOffset.UtcNow);
        d.RecordSuccess(200, DateTimeOffset.UtcNow);
        d.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        d.LastResponseStatusCode.Should().Be(200);
    }

    [Fact]
    public void Subscription_auto_deactivates_after_ten_consecutive_failures()
    {
        var sub = NewSubscription();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++)
        {
            sub.RecordDeliveryAttempt(succeeded: false, whenUtc: now);
        }
        sub.IsActive.Should().BeFalse();
        sub.ConsecutiveFailureCount.Should().Be(10);
    }

    [Fact]
    public void Success_resets_consecutive_failure_count()
    {
        var sub = NewSubscription();
        sub.RecordDeliveryAttempt(false, DateTimeOffset.UtcNow);
        sub.RecordDeliveryAttempt(false, DateTimeOffset.UtcNow);
        sub.RecordDeliveryAttempt(true, DateTimeOffset.UtcNow);
        sub.ConsecutiveFailureCount.Should().Be(0);
    }

    [Fact]
    public void Subscription_ctor_rejects_non_https()
    {
        var act = () => new WebhookSubscription(
            Guid.CreateVersion7(), "http://example.com/hook",
            "application.stage_changed", "hash", "prefix", "auth0|admin");
        act.Should().Throw<ArgumentException>().WithMessage("*https*");
    }

    private static WebhookDelivery NewDelivery() =>
        new(
            tenantId: Guid.CreateVersion7(),
            subscriptionId: Guid.CreateVersion7(),
            eventType: "application.stage_changed",
            payloadJson: "{}");

    private static WebhookSubscription NewSubscription() =>
        new(
            tenantId: Guid.CreateVersion7(),
            targetUrl: "https://customer.example.com/hooks/quantam",
            eventTypes: "application.stage_changed,offer.accepted",
            secretHash: "deadbeef",
            secretPrefix: "whsec_dead",
            createdByAuthSubject: "auth0|admin");
}
