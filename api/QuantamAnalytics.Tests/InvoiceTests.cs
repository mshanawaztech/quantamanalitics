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
        var act = () => invoice.UpdateDraft(20m, 1500m, null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Ctor_validates_period_order()
    {
        var act = () => new Invoice(
            tenantId: Guid.CreateVersion7(),
            contractorAuthSubject: "auth0|c",
            contractorEmail: "c@example.com",
            periodStartUtc: new DateOnly(2026, 5, 10),
            periodEndUtc: new DateOnly(2026, 5, 4),
            hours: 40, amount: 4000, currency: "USD", notes: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_validates_non_negative_amounts()
    {
        var act = () => new Invoice(
            tenantId: Guid.CreateVersion7(),
            contractorAuthSubject: "auth0|c",
            contractorEmail: "c@example.com",
            periodStartUtc: new DateOnly(2026, 5, 4),
            periodEndUtc: new DateOnly(2026, 5, 10),
            hours: -1, amount: 0, currency: "USD", notes: null);

        act.Should().Throw<ArgumentException>();
    }

    private static Invoice NewInvoice() => new(
        tenantId: Guid.CreateVersion7(),
        contractorAuthSubject: "auth0|contractor",
        contractorEmail: "contractor@example.com",
        periodStartUtc: new DateOnly(2026, 5, 4),
        periodEndUtc: new DateOnly(2026, 5, 10),
        hours: 40m,
        amount: 4000m,
        currency: "usd",
        notes: "Standard week");
}
