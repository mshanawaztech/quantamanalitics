using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Phase 9 / Story 72 — billing aggregate, one per tenant. Tracks plan,
/// purchased seats, and trial / paid status. The real Stripe sync lives
/// in Phase 5 (PR-43 `qa001-stripe-subscriptions`); this aggregate is
/// the source of truth that flow writes into.
///
/// State machine:
///
///   Trialing → (Active | Cancelled | PastDue) → Cancelled
///
/// Trialing starts at tenant creation with a 14-day window. Active is
/// what Stripe webhook callers flip us into after a successful charge.
/// PastDue is the recoverable failure state (next attempt incoming);
/// Cancelled is terminal.
/// </summary>
public sealed class TenantSubscription : ITenantScoped
{
    private TenantSubscription() { }

    public TenantSubscription(
        Guid tenantId,
        string planCode,
        int includedSeats,
        decimal pricePerSeat,
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (includedSeats < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(includedSeats), "Seat count must be positive.");
        }
        if (pricePerSeat < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pricePerSeat), "Price-per-seat must be non-negative.");
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        PlanCode = planCode.Trim();
        IncludedSeats = includedSeats;
        PricePerSeat = pricePerSeat;
        Currency = currency.Trim().ToUpperInvariant();
        Status = TenantSubscriptionStatus.Trialing;
        TrialEndsAtUtc = DateTimeOffset.UtcNow.AddDays(14);
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string PlanCode { get; private set; } = default!;
    public int IncludedSeats { get; private set; }
    public decimal PricePerSeat { get; private set; }
    public string Currency { get; private set; } = default!;
    public TenantSubscriptionStatus Status { get; private set; }
    public DateTimeOffset? TrialEndsAtUtc { get; private set; }
    public DateTimeOffset? CurrentPeriodEndsAtUtc { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Activate(string stripeSubscriptionId, DateTimeOffset currentPeriodEndsAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stripeSubscriptionId);
        Status = TenantSubscriptionStatus.Active;
        StripeSubscriptionId = stripeSubscriptionId.Trim();
        CurrentPeriodEndsAtUtc = currentPeriodEndsAtUtc;
        TrialEndsAtUtc = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void AttachStripeCustomer(string stripeCustomerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stripeCustomerId);
        StripeCustomerId = stripeCustomerId.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkPastDue()
    {
        if (Status != TenantSubscriptionStatus.Active)
        {
            throw new InvalidOperationException(
                "Only Active subscriptions can transition to PastDue.");
        }
        Status = TenantSubscriptionStatus.PastDue;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Reactivate(DateTimeOffset currentPeriodEndsAtUtc)
    {
        if (Status != TenantSubscriptionStatus.PastDue)
        {
            throw new InvalidOperationException(
                "Reactivate is only valid from PastDue.");
        }
        Status = TenantSubscriptionStatus.Active;
        CurrentPeriodEndsAtUtc = currentPeriodEndsAtUtc;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (Status == TenantSubscriptionStatus.Cancelled)
        {
            return; // idempotent
        }
        Status = TenantSubscriptionStatus.Cancelled;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ChangeSeatCount(int includedSeats)
    {
        if (includedSeats < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(includedSeats), "Seat count must be positive.");
        }
        IncludedSeats = includedSeats;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// True when the platform should still let the tenant in — Trialing
    /// (inside the window), Active, or PastDue. Cancelled and
    /// trial-expired both lock them out.
    /// </summary>
    public bool IsEntitled(DateTimeOffset asOfUtc) =>
        Status switch
        {
            TenantSubscriptionStatus.Active => true,
            TenantSubscriptionStatus.PastDue => true,
            TenantSubscriptionStatus.Trialing => TrialEndsAtUtc is null || TrialEndsAtUtc > asOfUtc,
            _ => false,
        };
}

public enum TenantSubscriptionStatus
{
    Trialing = 0,
    Active = 1,
    PastDue = 2,
    Cancelled = 3,
}
