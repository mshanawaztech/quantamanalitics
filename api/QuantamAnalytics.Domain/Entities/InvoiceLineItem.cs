using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// A single billable line on an <see cref="Invoice"/>. An invoice is the sum
/// of its line items; pre-tax totals are stored denormalized on the parent
/// invoice so list queries stay cheap, but the line items are the source of
/// truth for what the contractor is charging for.
/// </summary>
/// <remarks>
/// Line items are owned by their parent invoice — they cascade-delete with
/// the invoice and are never reachable across tenants because the invoice
/// already enforces the tenant filter.
/// </remarks>
public sealed class InvoiceLineItem
{
    private InvoiceLineItem() { }

    public InvoiceLineItem(
        Guid invoiceId,
        string description,
        decimal hours,
        decimal rate,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (hours < 0)
        {
            throw new ArgumentException("Hours must not be negative.", nameof(hours));
        }
        if (rate < 0)
        {
            throw new ArgumentException("Rate must not be negative.", nameof(rate));
        }
        if (sortOrder < 0)
        {
            throw new ArgumentException("Sort order must not be negative.", nameof(sortOrder));
        }

        Id = Guid.CreateVersion7();
        InvoiceId = invoiceId;
        Description = description.Trim();
        Hours = hours;
        Rate = rate;
        // Round the per-line amount to 2dp so the persisted value matches
        // what the contractor sees on screen. Aggregating these into the
        // invoice subtotal then sums exact values (no rounding drift).
        Amount = Math.Round(hours * rate, 2, MidpointRounding.AwayFromZero);
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }

    /// <summary>Free-text label, e.g. "Consulting services — week of May 1."</summary>
    public string Description { get; private set; } = default!;

    /// <summary>Hours billed on this line. Decimal so partial hours work.</summary>
    public decimal Hours { get; private set; }

    /// <summary>Hourly rate in the invoice's currency.</summary>
    public decimal Rate { get; private set; }

    /// <summary>Computed from <c>hours * rate</c> at construction; never set independently.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Display order. 0-indexed, contiguous within an invoice.</summary>
    public int SortOrder { get; private set; }
}
