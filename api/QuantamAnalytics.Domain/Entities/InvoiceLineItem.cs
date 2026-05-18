using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// A single billable line on an <see cref="Invoice"/>. v4 reshapes this
/// from a flat (description, hours, rate) triplet into a week-based
/// engagement: each line is one work-week (Monday → Sunday) with an
/// optional notes column. Hours are computed from <c>DaysWorked × HoursPerDay</c>
/// so the PDF can render the familiar <c>"5×8=40"</c> formula contractors
/// expect to see on their invoices.
/// </summary>
/// <remarks>
/// Line items are owned by their parent invoice — they cascade-delete with
/// the invoice and are never reachable across tenants because the invoice
/// already enforces the tenant filter.
///
/// Backward compatibility: pre-v4 line items had no week range / days /
/// hours-per-day. The migration backfill seeds <see cref="DaysWorked"/> = 0
/// and <see cref="HoursPerDay"/> = <see cref="Hours"/> for those rows so
/// existing data still adds up. New line items always use the full shape.
/// </remarks>
public sealed class InvoiceLineItem
{
    private InvoiceLineItem() { }

    public InvoiceLineItem(
        Guid invoiceId,
        string description,
        DateOnly? weekStartUtc,
        decimal daysWorked,
        decimal hoursPerDay,
        decimal rate,
        string? notes,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (daysWorked < 0)
        {
            throw new ArgumentException("Days worked must not be negative.", nameof(daysWorked));
        }
        if (hoursPerDay < 0)
        {
            throw new ArgumentException("Hours per day must not be negative.", nameof(hoursPerDay));
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
        WeekStartUtc = weekStartUtc;
        WeekEndUtc = weekStartUtc?.AddDays(6); // Mon → Sun
        DaysWorked = daysWorked;
        HoursPerDay = hoursPerDay;
        Rate = rate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        // Hours computed from the day/hour formula so the invoice arithmetic
        // is exact and the PDF can render "5×8=40" without re-deriving it.
        Hours = Math.Round(daysWorked * hoursPerDay, 2, MidpointRounding.AwayFromZero);
        Amount = Math.Round(Hours * rate, 2, MidpointRounding.AwayFromZero);
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }

    /// <summary>Free-text label, e.g. "NYS-DOCCS — billing week."</summary>
    public string Description { get; private set; } = default!;

    /// <summary>Monday of the week this line covers. Null for legacy / non-weekly lines.</summary>
    public DateOnly? WeekStartUtc { get; private set; }

    /// <summary>Sunday of the week — auto-derived from <see cref="WeekStartUtc"/>.</summary>
    public DateOnly? WeekEndUtc { get; private set; }

    /// <summary>Days actually worked this week (0–7 typically; allow fractional).</summary>
    public decimal DaysWorked { get; private set; }

    /// <summary>Typical hours per day (e.g., 8.0). Decimal so 7.5 / 6.25 work.</summary>
    public decimal HoursPerDay { get; private set; }

    /// <summary>Computed: <c>DaysWorked × HoursPerDay</c>, rounded to 2dp.</summary>
    public decimal Hours { get; private set; }

    /// <summary>Hourly rate in the invoice's currency.</summary>
    public decimal Rate { get; private set; }

    /// <summary>Computed: <c>Hours × Rate</c>, rounded to 2dp.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Optional per-line notes (e.g., overtime context, vacation day taken).</summary>
    public string? Notes { get; private set; }

    /// <summary>Display order. 0-indexed, contiguous within an invoice.</summary>
    public int SortOrder { get; private set; }
}
