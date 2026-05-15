using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Phase 9 / Story 71 — outbound webhook subscription per tenant.
/// SecretHash is the SHA-256 of the secret used to HMAC-sign the
/// delivery body; plaintext is shown once at create time.
///
/// EventTypes is a comma-separated allow-list (e.g.
/// <c>"application.stage_changed,offer.accepted"</c>). The delivery worker
/// joins on this to filter which events go where.
/// </summary>
public sealed class WebhookSubscription : ITenantScoped
{
    private WebhookSubscription() { }

    public WebhookSubscription(
        Guid tenantId,
        string targetUrl,
        string eventTypes,
        string secretHash,
        string secretPrefix,
        string createdByAuthSubject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUrl);
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "Webhook targets must be absolute https URLs.",
                nameof(targetUrl));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypes);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByAuthSubject);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        TargetUrl = targetUrl.Trim();
        EventTypes = eventTypes.Trim();
        SecretHash = secretHash;
        SecretPrefix = secretPrefix;
        CreatedByAuthSubject = createdByAuthSubject.Trim();
        IsActive = true;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string TargetUrl { get; private set; } = default!;
    public string EventTypes { get; private set; } = default!;
    public string SecretHash { get; private set; } = default!;
    public string SecretPrefix { get; private set; } = default!;
    public string CreatedByAuthSubject { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastDeliveryAttemptUtc { get; private set; }
    public DateTimeOffset? LastSuccessfulDeliveryUtc { get; private set; }
    public int ConsecutiveFailureCount { get; private set; }

    public void Deactivate() => IsActive = false;
    public void Reactivate()
    {
        IsActive = true;
        ConsecutiveFailureCount = 0;
    }

    public void RecordDeliveryAttempt(bool succeeded, DateTimeOffset whenUtc)
    {
        LastDeliveryAttemptUtc = whenUtc;
        if (succeeded)
        {
            LastSuccessfulDeliveryUtc = whenUtc;
            ConsecutiveFailureCount = 0;
        }
        else
        {
            ConsecutiveFailureCount++;
            // Auto-deactivate after 10 consecutive failures to stop
            // hammering a broken endpoint. The user reactivates by hand.
            if (ConsecutiveFailureCount >= 10)
            {
                IsActive = false;
            }
        }
    }
}

/// <summary>
/// One queued attempt to deliver an event to a subscription. The worker
/// process pulls Pending rows in FIFO order and either marks them
/// Delivered or schedules a retry with exponential backoff.
/// </summary>
public sealed class WebhookDelivery : ITenantScoped
{
    private WebhookDelivery() { }

    public WebhookDelivery(
        Guid tenantId,
        Guid subscriptionId,
        string eventType,
        string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        EventType = eventType.Trim();
        PayloadJson = payloadJson;
        Status = WebhookDeliveryStatus.Pending;
        NextAttemptAtUtc = DateTimeOffset.UtcNow;
        AttemptCount = 0;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public string EventType { get; private set; } = default!;
    public string PayloadJson { get; private set; } = default!;
    public WebhookDeliveryStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? LastAttemptedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public int? LastResponseStatusCode { get; private set; }
    public string? LastResponseBody { get; private set; }

    public void RecordSuccess(int responseStatusCode, DateTimeOffset whenUtc)
    {
        Status = WebhookDeliveryStatus.Delivered;
        LastAttemptedAtUtc = whenUtc;
        AttemptCount++;
        LastResponseStatusCode = responseStatusCode;
    }

    public void RecordFailure(int responseStatusCode, string? responseBody, DateTimeOffset whenUtc)
    {
        AttemptCount++;
        LastAttemptedAtUtc = whenUtc;
        LastResponseStatusCode = responseStatusCode;
        LastResponseBody = string.IsNullOrEmpty(responseBody)
            ? null
            : (responseBody.Length > 2000 ? responseBody[..2000] : responseBody);

        if (AttemptCount >= 8)
        {
            Status = WebhookDeliveryStatus.Failed;
            return;
        }

        // Exponential backoff: 1m, 2m, 4m, 8m, 16m, 32m, 64m, 128m.
        var minutes = (int)Math.Pow(2, AttemptCount - 1);
        NextAttemptAtUtc = whenUtc.AddMinutes(minutes);
        Status = WebhookDeliveryStatus.Pending;
    }
}

public enum WebhookDeliveryStatus
{
    Pending = 0,
    Delivered = 1,
    Failed = 2,
}
