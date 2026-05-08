using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Tenant-scoped invoice raised by a contractor for a billing period.
/// Mirrors the timesheet flow but for direct billing — used when a
/// contractor bills the staffing firm directly rather than going through
/// payroll. Phase 6 ships the request shape, the state machine, and the
/// client-side approval surface; the QuickBooks / Stripe handoff already
/// has a baseline that this plugs into without further code changes.
/// </summary>
/// <remarks>
/// State machine:
///   Draft → Submitted → (Approved | Rejected); Approved → Paid.
/// Late mutations from a Paid or Rejected terminal state throw —
/// recovering from a wrong terminal status is an explicit "open a new
/// invoice" workflow, not an in-place override.
/// </remarks>
public sealed class Invoice : ITenantScoped
{
    private Invoice() { }

    public Invoice(
        Guid tenantId,
        string contractorAuthSubject,
        string contractorEmail,
        DateOnly periodStartUtc,
        DateOnly periodEndUtc,
        decimal hours,
        decimal amount,
        string currency,
        string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractorAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractorEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (periodEndUtc < periodStartUtc)
        {
            throw new ArgumentException(
                "Invoice period_end must not be earlier than period_start.",
                nameof(periodEndUtc));
        }

        if (hours < 0)
        {
            throw new ArgumentException("Hours must not be negative.", nameof(hours));
        }
        if (amount < 0)
        {
            throw new ArgumentException("Amount must not be negative.", nameof(amount));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ContractorAuthSubject = contractorAuthSubject.Trim();
        ContractorEmail = contractorEmail.Trim().ToLowerInvariant();
        PeriodStartUtc = periodStartUtc;
        PeriodEndUtc = periodEndUtc;
        Hours = hours;
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = InvoiceStatus.Draft;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ContractorAuthSubject { get; private set; } = default!;
    public string ContractorEmail { get; private set; } = default!;

    public DateOnly PeriodStartUtc { get; private set; }
    public DateOnly PeriodEndUtc { get; private set; }

    /// <summary>Hours billed in the period. Decimal so partial hours work.</summary>
    public decimal Hours { get; private set; }

    /// <summary>Invoice amount in <see cref="Currency"/>.</summary>
    public decimal Amount { get; private set; }

    /// <summary>ISO-4217 currency code, uppercased.</summary>
    public string Currency { get; private set; } = default!;

    public string? Notes { get; private set; }

    /// <summary>Auth subject of whoever made the most recent decision.</summary>
    public string? ReviewedByAuthSubject { get; private set; }
    public string? ReviewerNote { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void UpdateDraft(decimal hours, decimal amount, string? notes)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Only draft invoices can be edited. Current status: {Status}.");
        }
        if (hours < 0)
        {
            throw new ArgumentException("Hours must not be negative.", nameof(hours));
        }
        if (amount < 0)
        {
            throw new ArgumentException("Amount must not be negative.", nameof(amount));
        }

        Hours = hours;
        Amount = amount;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Submit()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Only draft invoices can be submitted. Current status: {Status}.");
        }

        Status = InvoiceStatus.Submitted;
        SubmittedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = SubmittedAtUtc.Value;
    }

    public void Approve(string reviewerAuthSubject, string? note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerAuthSubject);
        if (Status != InvoiceStatus.Submitted)
        {
            throw new InvalidOperationException(
                $"Only submitted invoices can be approved. Current status: {Status}.");
        }

        Status = InvoiceStatus.Approved;
        ReviewedByAuthSubject = reviewerAuthSubject.Trim();
        ReviewerNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ReviewedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = ReviewedAtUtc.Value;
    }

    public void Reject(string reviewerAuthSubject, string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(note);
        if (Status != InvoiceStatus.Submitted)
        {
            throw new InvalidOperationException(
                $"Only submitted invoices can be rejected. Current status: {Status}.");
        }

        Status = InvoiceStatus.Rejected;
        ReviewedByAuthSubject = reviewerAuthSubject.Trim();
        ReviewerNote = note.Trim();
        ReviewedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = ReviewedAtUtc.Value;
    }

    public void MarkPaid()
    {
        if (Status != InvoiceStatus.Approved)
        {
            throw new InvalidOperationException(
                $"Only approved invoices can be marked paid. Current status: {Status}.");
        }

        Status = InvoiceStatus.Paid;
        PaidAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = PaidAtUtc.Value;
    }
}

public enum InvoiceStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Paid = 4,
}
