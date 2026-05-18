using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Tenant-scoped invoice raised by a contractor for a billing period.
/// Mirrors the timesheet flow but for direct billing — used when a
/// contractor bills the staffing firm directly rather than going through
/// payroll. Phase 6 shipped the request shape, the state machine, and the
/// client-side approval surface; v2 (Invoices polish) extends the entity
/// with multi-line items, issue/due dates, an auto-minted invoice number,
/// and a tax line.
/// </summary>
/// <remarks>
/// State machine:
///   Draft → Submitted → (Approved | Rejected); Approved → Paid.
/// Late mutations from a Paid or Rejected terminal state throw —
/// recovering from a wrong terminal status is an explicit "open a new
/// invoice" workflow, not an in-place override.
///
/// v2 fields are non-nullable on new rows but the migration backfills
/// existing data so old single-line invoices keep working:
/// - InvoiceNumber: synthesized for legacy rows during the migration
/// - IssueDateUtc: defaults to CreatedAtUtc.Date for legacy rows
/// - DueDateUtc: defaults to IssueDateUtc + 30 days for legacy rows
/// - ClientName: defaults to "" for legacy rows
/// - Subtotal / TaxRate / TaxAmount: legacy rows get Subtotal = Amount,
///   TaxRate = 0, TaxAmount = 0
/// - LineItems: legacy rows get a single synthetic item on read.
/// </remarks>
public sealed class Invoice : ITenantScoped
{
    private readonly List<InvoiceLineItem> _lineItems = [];

    private Invoice() { }

    public Invoice(
        Guid tenantId,
        string contractorAuthSubject,
        string contractorEmail,
        string invoiceNumber,
        string clientName,
        DateOnly issueDateUtc,
        DateOnly dueDateUtc,
        DateOnly periodStartUtc,
        DateOnly periodEndUtc,
        string currency,
        decimal taxRate,
        IReadOnlyCollection<InvoiceLineItemInput> lineItems,
        string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractorAuthSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractorEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentNullException.ThrowIfNull(clientName);
        ArgumentNullException.ThrowIfNull(lineItems);

        if (lineItems.Count == 0)
        {
            throw new ArgumentException("An invoice must have at least one line item.", nameof(lineItems));
        }
        if (periodEndUtc < periodStartUtc)
        {
            throw new ArgumentException(
                "Invoice period_end must not be earlier than period_start.",
                nameof(periodEndUtc));
        }
        if (dueDateUtc < issueDateUtc)
        {
            throw new ArgumentException(
                "Invoice due_date must not be earlier than issue_date.",
                nameof(dueDateUtc));
        }
        if (taxRate < 0 || taxRate > 100)
        {
            throw new ArgumentException(
                "Tax rate must be a percentage between 0 and 100.",
                nameof(taxRate));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ContractorAuthSubject = contractorAuthSubject.Trim();
        ContractorEmail = contractorEmail.Trim().ToLowerInvariant();
        InvoiceNumber = invoiceNumber.Trim();
        ClientName = clientName.Trim();
        IssueDateUtc = issueDateUtc;
        DueDateUtc = dueDateUtc;
        PeriodStartUtc = periodStartUtc;
        PeriodEndUtc = periodEndUtc;
        Currency = currency.Trim().ToUpperInvariant();
        TaxRate = taxRate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = InvoiceStatus.Draft;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;

        ReplaceLineItems(lineItems);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ContractorAuthSubject { get; private set; } = default!;
    public string ContractorEmail { get; private set; } = default!;

    /// <summary>Human-readable invoice number (e.g. <c>INV-2026-0001</c>). Per-tenant unique.</summary>
    public string InvoiceNumber { get; private set; } = default!;

    /// <summary>
    /// Free-text label for the billed client. v2 ships with this as a
    /// denormalized string so the v2 endpoint can stay schema-stable while
    /// the Client entity is built in a later iteration.
    /// </summary>
    public string ClientName { get; private set; } = default!;

    /// <summary>Date the invoice is issued. Different from <see cref="PeriodStartUtc"/>.</summary>
    public DateOnly IssueDateUtc { get; private set; }

    /// <summary>Date payment is due.</summary>
    public DateOnly DueDateUtc { get; private set; }

    public DateOnly PeriodStartUtc { get; private set; }
    public DateOnly PeriodEndUtc { get; private set; }

    /// <summary>Sum of hours across line items. Denormalized for list queries.</summary>
    public decimal Hours { get; private set; }

    /// <summary>Sum of line-item amounts before tax. Denormalized.</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>Tax percentage applied to the subtotal (0–100). 0 means no tax line.</summary>
    public decimal TaxRate { get; private set; }

    /// <summary>Computed: round(<see cref="Subtotal"/> × <see cref="TaxRate"/> / 100, 2).</summary>
    public decimal TaxAmount { get; private set; }

    /// <summary>Total billed amount: <see cref="Subtotal"/> + <see cref="TaxAmount"/>.</summary>
    public decimal Amount { get; private set; }

    /// <summary>ISO-4217 currency code, uppercased.</summary>
    public string Currency { get; private set; } = default!;

    public string? Notes { get; private set; }

    /// <summary>Line items belonging to this invoice. Cascade-deleted with the parent.</summary>
    public IReadOnlyList<InvoiceLineItem> LineItems => _lineItems;

    /// <summary>Auth subject of whoever made the most recent decision.</summary>
    public string? ReviewedByAuthSubject { get; private set; }
    public string? ReviewerNote { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Replaces line items + scalar metadata while the invoice is still a Draft.
    /// </summary>
    public void UpdateDraft(
        string clientName,
        DateOnly issueDateUtc,
        DateOnly dueDateUtc,
        DateOnly periodStartUtc,
        DateOnly periodEndUtc,
        string currency,
        decimal taxRate,
        IReadOnlyCollection<InvoiceLineItemInput> lineItems,
        string? notes)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Only draft invoices can be edited. Current status: {Status}.");
        }
        ArgumentNullException.ThrowIfNull(clientName);
        ArgumentNullException.ThrowIfNull(lineItems);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (lineItems.Count == 0)
        {
            throw new ArgumentException("An invoice must have at least one line item.", nameof(lineItems));
        }
        if (periodEndUtc < periodStartUtc)
        {
            throw new ArgumentException(
                "Invoice period_end must not be earlier than period_start.",
                nameof(periodEndUtc));
        }
        if (dueDateUtc < issueDateUtc)
        {
            throw new ArgumentException(
                "Invoice due_date must not be earlier than issue_date.",
                nameof(dueDateUtc));
        }
        if (taxRate < 0 || taxRate > 100)
        {
            throw new ArgumentException("Tax rate must be a percentage between 0 and 100.", nameof(taxRate));
        }

        ClientName = clientName.Trim();
        IssueDateUtc = issueDateUtc;
        DueDateUtc = dueDateUtc;
        PeriodStartUtc = periodStartUtc;
        PeriodEndUtc = periodEndUtc;
        Currency = currency.Trim().ToUpperInvariant();
        TaxRate = taxRate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        ReplaceLineItems(lineItems);
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

    /// <summary>
    /// Replace the line items collection and recompute the denormalized
    /// totals (<see cref="Hours"/>, <see cref="Subtotal"/>, <see cref="TaxAmount"/>,
    /// <see cref="Amount"/>) from the new set. Callers pass minimal value
    /// objects; the entity instantiates the <see cref="InvoiceLineItem"/>
    /// children itself so it controls their lifecycle and the SortOrder
    /// stays contiguous.
    /// </summary>
    private void ReplaceLineItems(IReadOnlyCollection<InvoiceLineItemInput> inputs)
    {
        _lineItems.Clear();

        var sort = 0;
        foreach (var input in inputs)
        {
            _lineItems.Add(new InvoiceLineItem(
                invoiceId: Id,
                description: input.Description,
                weekStartUtc: input.WeekStartUtc,
                daysWorked: input.DaysWorked,
                hoursPerDay: input.HoursPerDay,
                rate: input.Rate,
                notes: input.Notes,
                sortOrder: sort++));
        }

        Hours = _lineItems.Sum(x => x.Hours);
        Subtotal = _lineItems.Sum(x => x.Amount);
        TaxAmount = Math.Round(Subtotal * TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
        Amount = Subtotal + TaxAmount;
    }
}

/// <summary>
/// Value-object input for adding a line item via the Invoice aggregate. v4
/// is week-based: callers specify <see cref="DaysWorked"/> × <see cref="HoursPerDay"/>
/// and the entity computes Hours, Amount, and the Sunday end-of-week date.
/// Pre-v4 callers can pass <see cref="WeekStartUtc"/> = null and stuff the
/// total hours into <see cref="HoursPerDay"/> with <see cref="DaysWorked"/> = 1
/// to keep backward compatibility.
/// </summary>
public sealed record InvoiceLineItemInput(
    string Description,
    DateOnly? WeekStartUtc,
    decimal DaysWorked,
    decimal HoursPerDay,
    decimal Rate,
    string? Notes);

public enum InvoiceStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Paid = 4,
}
