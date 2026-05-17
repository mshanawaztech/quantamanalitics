using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Tests;

public sealed class InvoiceTests
{
    [Fact]
    public void Ctor_starts_in_Draft()
    {
        var invoice = NewInvoice();
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.SubmittedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Ctor_assigns_invoice_number_and_client_name()
    {
        var invoice = NewInvoice();
        invoice.InvoiceNumber.Should().Be("INV-2026-0001");
        invoice.ClientName.Should().Be("Acme Corporation");
    }

    [Fact]
    public void Ctor_computes_subtotal_tax_and_total_from_line_items()
    {
        var invoice = new Invoice(
            tenantId: Guid.CreateVersion7(),
            contractorAuthSubject: "auth0|c",
            contractorEmail: "c@example.com",
            invoiceNumber: "INV-2026-0001",
            clientName: "Acme",
            issueDateUtc: new DateOnly(2026, 5, 1),
            dueDateUtc: new DateOnly(2026, 5, 30),
            periodStartUtc: new DateOnly(2026, 5, 1),
            periodEndUtc: new DateOnly(2026, 5, 15),
            currency: "USD",
            taxRate: 10m,
            lineItems: new[]
            {
                new InvoiceLineItemInput("Consulting", 10m, 100m),  // $1000
                new InvoiceLineItemInput("Workshop",  5m, 200m),     // $1000
            },
            notes: null);

        invoice.Hours.Should().Be(15m);
        invoice.Subtotal.Should().Be(2000m);
        invoice.TaxAmount.Should().Be(200m);
        invoice.Amount.Should().Be(2200m);
        invoice.LineItems.Should().HaveCount(2);
        invoice.LineItems[0].Amount.Should().Be(1000m);
        invoice.LineItems[1].Amount.Should().Be(1000m);
    }

    [Fact]
    public void Submit_transitions_to_Submitted()
    {
        var invoice = NewInvoice();
        invoice.Submit();
        invoice.Status.Should().Be(InvoiceStatus.Submitted);
        invoice.SubmittedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Submit_throws_when_not_draft()
    {
        var invoice = NewInvoice();
        invoice.Submit();
        var act = () => invoice.Submit();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_requires_submitted_state()
    {
        var invoice = NewInvoice();
        var act = () => invoice.Approve("auth0|reviewer", "looks good");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_records_reviewer_and_note()
    {
        var invoice = NewInvoice();
        invoice.Submit();
        invoice.Approve("auth0|reviewer", "looks good");
        invoice.Status.Should().Be(InvoiceStatus.Approved);
        invoice.ReviewedByAuthSubject.Should().Be("auth0|reviewer");
        invoice.ReviewerNote.Should().Be("looks good");
    }

    [Fact]
    public void Reject_requires_a_note()
    {
        var invoice = NewInvoice();
        invoice.Submit();
        var act = () => invoice.Reject("auth0|reviewer", string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reject_terminal_blocks_further_state_changes()
    {
        var invoice = NewInvoice();
        invoice.Submit();
        invoice.Reject("auth0|reviewer", "missing receipts");

        var actSubmit = () => invoice.Submit();
        var actApprove = () => invoice.Approve("auth0|reviewer", "");
        actSubmit.Should().Throw<InvalidOperationException>();
        actApprove.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkPaid_requires_approved_state()
    {
        var invoice = NewInvoice();
        var act = () => invoice.MarkPaid();
        act.Should().Throw<InvalidOperationException>();

        invoice.Submit();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkPaid_after_approve_completes_lifecycle()
    {
        var invoice = NewInvoice();
        invoice.Submit();
        invoice.Approve("auth0|reviewer", null);
        invoice.MarkPaid();
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void UpdateDraft_throws_after_submit()
    {
        var invoice = NewInvoice();
        invoice.Submit();
        var act = () => invoice.UpdateDraft(
            clientName: "Acme",
            issueDateUtc: new DateOnly(2026, 5, 1),
            dueDateUtc: new DateOnly(2026, 5, 30),
            periodStartUtc: new DateOnly(2026, 5, 1),
            periodEndUtc: new DateOnly(2026, 5, 15),
            currency: "USD",
            taxRate: 0m,
            lineItems: new[] { new InvoiceLineItemInput("Hours", 10m, 100m) },
            notes: null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateDraft_replaces_line_items_and_recomputes_totals()
    {
        var invoice = NewInvoice();
        invoice.UpdateDraft(
            clientName: "Acme",
            issueDateUtc: new DateOnly(2026, 5, 1),
            dueDateUtc: new DateOnly(2026, 5, 30),
            periodStartUtc: new DateOnly(2026, 5, 1),
            periodEndUtc: new DateOnly(2026, 5, 15),
            currency: "USD",
            taxRate: 20m,
            lineItems: new[]
            {
                new InvoiceLineItemInput("Consulting", 5m, 200m), // 1000
            },
            notes: null);

        invoice.Subtotal.Should().Be(1000m);
        invoice.TaxAmount.Should().Be(200m);
        invoice.Amount.Should().Be(1200m);
        invoice.LineItems.Should().HaveCount(1);
    }

    [Fact]
    public void Ctor_validates_period_order()
    {
        var act = () => new Invoice(
            tenantId: Guid.CreateVersion7(),
            contractorAuthSubject: "auth0|c",
            contractorEmail: "c@example.com",
            invoiceNumber: "INV-2026-0001",
            clientName: "Acme",
            issueDateUtc: new DateOnly(2026, 5, 1),
            dueDateUtc: new DateOnly(2026, 5, 30),
            periodStartUtc: new DateOnly(2026, 5, 10),
            periodEndUtc: new DateOnly(2026, 5, 4),
            currency: "USD",
            taxRate: 0m,
            lineItems: new[] { new InvoiceLineItemInput("Hours", 10m, 100m) },
            notes: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_validates_due_date_not_before_issue_date()
    {
        var act = () => new Invoice(
            tenantId: Guid.CreateVersion7(),
            contractorAuthSubject: "auth0|c",
            contractorEmail: "c@example.com",
            invoiceNumber: "INV-2026-0001",
            clientName: "Acme",
            issueDateUtc: new DateOnly(2026, 5, 30),
            dueDateUtc: new DateOnly(2026, 5, 1),
            periodStartUtc: new DateOnly(2026, 5, 1),
            periodEndUtc: new DateOnly(2026, 5, 15),
            currency: "USD",
            taxRate: 0m,
            lineItems: new[] { new InvoiceLineItemInput("Hours", 10m, 100m) },
            notes: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_rejects_empty_line_items_collection()
    {
        var act = () => new Invoice(
            tenantId: Guid.CreateVersion7(),
            contractorAuthSubject: "auth0|c",
            contractorEmail: "c@example.com",
            invoiceNumber: "INV-2026-0001",
            clientName: "Acme",
            issueDateUtc: new DateOnly(2026, 5, 1),
            dueDateUtc: new DateOnly(2026, 5, 30),
            periodStartUtc: new DateOnly(2026, 5, 1),
            periodEndUtc: new DateOnly(2026, 5, 15),
            currency: "USD",
            taxRate: 0m,
            lineItems: Array.Empty<InvoiceLineItemInput>(),
            notes: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_validates_non_negative_line_item_amounts()
    {
        var act = () => new Invoice(
            tenantId: Guid.CreateVersion7(),
            contractorAuthSubject: "auth0|c",
            contractorEmail: "c@example.com",
            invoiceNumber: "INV-2026-0001",
            clientName: "Acme",
            issueDateUtc: new DateOnly(2026, 5, 1),
            dueDateUtc: new DateOnly(2026, 5, 30),
            periodStartUtc: new DateOnly(2026, 5, 4),
            periodEndUtc: new DateOnly(2026, 5, 10),
            currency: "USD",
            taxRate: 0m,
            lineItems: new[] { new InvoiceLineItemInput("Bad", -1m, 100m) },
            notes: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NumberSequence_mints_incrementing_invoice_numbers()
    {
        var seq = new InvoiceNumberSequence(Guid.CreateVersion7(), 2026);
        seq.MintNext().Should().Be("INV-2026-0001");
        seq.MintNext().Should().Be("INV-2026-0002");
        seq.MintNext().Should().Be("INV-2026-0003");
        seq.NextValue.Should().Be(4);
    }

    private static Invoice NewInvoice() => new(
        tenantId: Guid.CreateVersion7(),
        contractorAuthSubject: "auth0|contractor",
        contractorEmail: "contractor@example.com",
        invoiceNumber: "INV-2026-0001",
        clientName: "Acme Corporation",
        issueDateUtc: new DateOnly(2026, 5, 1),
        dueDateUtc: new DateOnly(2026, 5, 30),
        periodStartUtc: new DateOnly(2026, 5, 4),
        periodEndUtc: new DateOnly(2026, 5, 10),
        currency: "usd",
        taxRate: 10m,
        lineItems: new[]
        {
            new InvoiceLineItemInput("Standard week", 40m, 100m),
        },
        notes: "Standard week");
}
