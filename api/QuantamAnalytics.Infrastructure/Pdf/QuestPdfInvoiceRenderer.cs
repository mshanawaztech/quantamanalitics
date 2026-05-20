using System.Globalization;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Domain.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QuantamAnalytics.Infrastructure.Pdf;

/// <summary>
/// Renders an invoice PDF using QuestPDF. Visual style matches the
/// reference invoice template: angled navy banner letterhead at the top
/// (with company identity), a "Invoice #NNNN" + bank remit-to block, the
/// rate / period / payment-due strip, an engagement-style line items
/// table, and a prominent grand total at the bottom right.
/// </summary>
public sealed class QuestPdfInvoiceRenderer : IInvoicePdfRenderer
{
    private const string DefaultPrimaryHex = "#1a2d5a";   // banner navy
    private const string DefaultAccentHex = "#e6c9a8";    // table header tan
    private const string MutedTextHex = "#5d6577";
    private const string BodyTextHex = "#1a1f2c";

    // License is registered once per process at the top of Program.cs in
    // QuantamAnalytics.Api. WebApplicationFactory<Program> re-enters that
    // file so the integration tests get the same registration.

    public byte[] Render(Invoice invoice, TenantBranding? branding, byte[]? logoBytes)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var primary = branding?.PrimaryColorHex ?? BrandingDefaults.PrimaryColorHex;
        var accent = branding?.AccentColorHex ?? BrandingDefaults.AccentColorHex;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(0); // banner runs edge-to-edge; content inset uses padding inside.
                page.DefaultTextStyle(t => t.FontFamily("Helvetica").FontSize(10).FontColor(BodyTextHex));

                page.Content().Column(col =>
                {
                    col.Item().Element(c => RenderBanner(c, invoice, branding, logoBytes, primary, accent));
                    col.Item().PaddingHorizontal(40).PaddingTop(20).Column(body =>
                    {
                        body.Item().Element(c => RenderInvoiceNumber(c, invoice, primary));
                        body.Item().PaddingTop(16).Element(c => RenderBankBlock(c, invoice, branding));
                        body.Item().PaddingTop(16).Element(c => RenderRatePeriodStrip(c, invoice, branding, primary));
                        body.Item().PaddingTop(24).Element(c => RenderItemsTable(c, invoice, accent));
                        body.Item().PaddingTop(8).Element(c => RenderGrandTotal(c, invoice, primary));
                        body.Item().PaddingTop(24).Element(c => RenderFooter(c, invoice));
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    // ── Banner letterhead (angled navy + accent stripe + identity) ──
    /// <summary>
    /// Inline brand mark — the analytics-magnifier glyph from the SPA's
    /// qa-logo, rendered in its on-dark form (white magnifier ring + white
    /// chart bars + gold trend line + gold handle, no hexagon) so it reads
    /// crisply against the navy invoice banner. Inlined as SVG so the PDF
    /// carries the mark without shipping a binary asset.
    /// </summary>
    private const string BrandMarkSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120">
          <line x1="80" y1="81" x2="93" y2="94" stroke="#F4CE54" stroke-width="12" stroke-linecap="round"/>
          <circle cx="58" cy="59" r="31" fill="none" stroke="#ffffff" stroke-width="7"/>
          <rect x="45" y="65" width="8" height="12" rx="2" fill="#ffffff"/>
          <rect x="56" y="59" width="8" height="18" rx="2" fill="#ffffff"/>
          <rect x="67" y="53" width="8" height="24" rx="2" fill="#ffffff"/>
          <polyline points="43,57 53,51 63,54 73,45" fill="none" stroke="#F4CE54" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
          <circle cx="43" cy="57" r="3" fill="#F4CE54"/>
          <circle cx="53" cy="51" r="3" fill="#F4CE54"/>
          <circle cx="63" cy="54" r="3" fill="#F4CE54"/>
          <circle cx="73" cy="45" r="3" fill="#F4CE54"/>
        </svg>
        """;

    private static void RenderBanner(
        IContainer container,
        Invoice invoice,
        TenantBranding? branding,
        byte[]? logoBytes,
        string primary,
        string accent)
    {
        // Banner: full-width navy fill, thin accent stripe along the
        // bottom, brand mark + identity text on the left, optional
        // tenant-uploaded logo on the right.
        container.Height(140).Layers(layers =>
        {
            // Full-width navy background
            layers.Layer().Background(primary);
            // Accent stripe across the bottom
            layers.Layer().AlignBottom().Height(6).Background(accent);

            // Identity — show the company (legal) name, not a person's name.
            // Falls back to platform defaults so a brand-new tenant still
            // gets a usable letterhead before visiting /settings/branding.
            // The banner stays clean: mark + company name + "INVOICE" label.
            // Contact + bank details live in the dedicated bank block below.
            var companyName = branding?.LegalName ?? BrandingDefaults.LegalName;

            layers.PrimaryLayer().PaddingLeft(40).PaddingVertical(28).Row(row =>
            {
                // Brand mark on the left. PaddingRight provides the gutter
                // between the mark and the identity column.
                row.ConstantItem(72).Height(60).AlignMiddle().PaddingRight(16).Svg(BrandMarkSvg);

                row.RelativeItem().AlignMiddle().Column(c =>
                {
                    c.Item().Text(companyName).FontSize(22).Bold().FontColor(Colors.White);
                    c.Item().PaddingTop(3).Text("INVOICE")
                        .FontSize(12).Bold().FontColor(accent);
                });
            });

            // Optional tenant logo, top-right — sits alongside the
            // platform mark and identity, doesn't replace them.
            if (logoBytes is not null && logoBytes.Length > 0)
            {
                layers.Layer().AlignRight().AlignTop().PaddingTop(24).PaddingRight(40)
                    .Height(72).Image(logoBytes);
            }
        });
    }

    private static void RenderInvoiceNumber(IContainer container, Invoice invoice, string primary)
    {
        container.Text("Invoice #" + invoice.InvoiceNumber)
            .FontSize(22).Bold().FontColor(primary);
    }

    private static void RenderBankBlock(IContainer container, Invoice invoice, TenantBranding? branding)
    {
        // Resolution order for each remit-to field:
        //   1. Per-invoice override on the Invoice entity (qa005)
        //   2. Tenant's TenantBranding row (if set in /settings/branding)
        //   3. Platform-owner defaults (BrandingDefaults) — Quantam's own values
        //
        // Per-invoice overrides let a contractor bill one client through a
        // different bank account without changing their tenant-wide branding.
        var bankName = invoice.RemitBankName
            ?? branding?.BankName ?? BrandingDefaults.BankName;
        var legalName = branding?.LegalName ?? BrandingDefaults.LegalName;
        var account = invoice.RemitAccountNumber
            ?? branding?.BankAccountNumber ?? BrandingDefaults.BankAccountNumber;
        var routing = invoice.RemitRoutingNumber
            ?? branding?.BankRoutingNumber ?? BrandingDefaults.BankRoutingNumber;

        container.Column(col =>
        {
            col.Item().Text("Bank Details").FontSize(13).Bold();

            if (!string.IsNullOrWhiteSpace(bankName))
            {
                col.Item().PaddingTop(6).Text("Bank Name: " + bankName).FontSize(11).Bold();
            }
            if (!string.IsNullOrWhiteSpace(legalName))
            {
                col.Item().PaddingTop(2).Text("Name: " + legalName).FontSize(11).Bold();
            }
            if (!string.IsNullOrWhiteSpace(account))
            {
                col.Item().PaddingTop(2).Text("Account number").FontSize(11).Bold();
                col.Item().Text(account).FontSize(11).Bold();
            }
            if (!string.IsNullOrWhiteSpace(routing))
            {
                col.Item().PaddingTop(2).Text("Routing number").FontSize(11).Bold();
                col.Item().Text(routing).FontSize(11).Bold();
            }
        });
    }

    private static void RenderRatePeriodStrip(IContainer container, Invoice invoice, TenantBranding? branding, string primary)
    {
        var hourlyRate = branding?.DefaultHourlyRate
            ?? (invoice.LineItems.Count > 0 ? invoice.LineItems[0].Rate : 0m);

        container.Column(col =>
        {
            // "Billed to" — surfaces the client name + optional vendor
            // reference so the recipient sees who the invoice was raised
            // against, not just an opaque invoice number. Skipped if
            // ClientName is empty (legacy/partial invoices).
            if (!string.IsNullOrWhiteSpace(invoice.ClientName))
            {
                col.Item().Text(t =>
                {
                    t.Span("Billed to: ").Bold().FontColor(primary);
                    t.Span(invoice.ClientName).Bold();
                    if (!string.IsNullOrWhiteSpace(invoice.VendorName))
                    {
                        t.Span($" / {invoice.VendorName}").FontColor(MutedTextHex);
                    }
                });
            }
            col.Item().PaddingTop(2).Text(t =>
            {
                t.Span("Hourly Rate: ").Bold().FontColor(primary);
                t.Span(FormatCurrency(invoice.Currency, hourlyRate)).Bold();
            });
            col.Item().PaddingTop(2).Text(t =>
            {
                t.Span("Date range of week being invoiced: ").Bold().FontColor(primary);
                t.Span($"{FormatDate(invoice.PeriodStartUtc)} - {FormatDate(invoice.PeriodEndUtc)}").Bold();
            });
            col.Item().PaddingTop(2).Text(t =>
            {
                t.Span("Payment due on ").Bold().FontColor(primary);
                t.Span(FormatLongDate(invoice.DueDateUtc) + ":").Bold();
            });
        });
    }

    private static void RenderItemsTable(IContainer container, Invoice invoice, string accent)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(5);   // Client Name / engagement
                c.RelativeColumn(2);   // Hrs. Worked
                c.RelativeColumn(2);   // Rate
                c.RelativeColumn(2);   // TOTAL
            });

            table.Header(header =>
            {
                // QuestPDF uses chainable .AlignCenter() on the container —
                // no TextAlignment enum exists. Headers are inlined here
                // (rather than a helper) so the strongly-typed lambda
                // parameter doesn't need to escape the local scope.
                StyleHeaderCell(header.Cell(), accent).Text("Client Name").Bold().FontSize(11);
                StyleHeaderCell(header.Cell(), accent).AlignCenter().Text("Hrs.\nWorked").Bold().FontSize(11);
                StyleHeaderCell(header.Cell(), accent).AlignCenter().Text("Rate").Bold().FontSize(11);
                StyleHeaderCell(header.Cell(), accent).AlignCenter().Text("TOTAL").Bold().FontSize(11);
            });

            foreach (var item in invoice.LineItems)
            {
                StyleBodyCell(table.Cell()).Text(item.Description).FontSize(10);
                StyleBodyCell(table.Cell()).AlignCenter().Text(FormatHoursFormula(item)).FontSize(10);
                StyleBodyCell(table.Cell()).AlignCenter().Text(FormatCurrency(invoice.Currency, item.Rate)).FontSize(10);
                StyleBodyCell(table.Cell()).AlignCenter().Text(FormatCurrency(invoice.Currency, item.Amount)).FontSize(10);
            }
        });
    }

    /// <summary>Shared header-cell styling: tan background, padded.</summary>
    private static IContainer StyleHeaderCell(IContainer cell, string accent) =>
        cell.Background(accent).Padding(8);

    /// <summary>Shared body-cell styling: bottom border, padded.</summary>
    private static IContainer StyleBodyCell(IContainer cell) =>
        cell.BorderBottom(0.5f).BorderColor("#e6e8ee").Padding(8);

    private static void RenderGrandTotal(IContainer container, Invoice invoice, string primary)
    {
        container.Background("#f4f6fa").Padding(12).AlignRight().Text(t =>
        {
            t.Span(FormatCurrency(invoice.Currency, invoice.Amount))
                .FontSize(22).Bold().FontColor(primary);
        });
    }

    private static void RenderFooter(IContainer container, Invoice invoice)
    {
        if (string.IsNullOrWhiteSpace(invoice.Notes)) return;

        container.PaddingTop(8).BorderTop(0.5f).BorderColor("#e6e8ee").PaddingTop(8).Column(c =>
        {
            c.Item().Text("Notes").FontSize(10).Bold().FontColor(MutedTextHex);
            c.Item().PaddingTop(2).Text(invoice.Notes!).FontSize(10).FontColor(MutedTextHex);
        });
    }

    // ── Formatting helpers ───────────────────────────────────────────
    private static string FormatCurrency(string currency, decimal amount) =>
        currency.ToUpperInvariant() switch
        {
            "USD" => "$" + amount.ToString("N2", CultureInfo.InvariantCulture),
            "EUR" => "€" + amount.ToString("N2", CultureInfo.InvariantCulture),
            "GBP" => "£" + amount.ToString("N2", CultureInfo.InvariantCulture),
            _ => $"{currency} {amount.ToString("N2", CultureInfo.InvariantCulture)}",
        };

    private static string FormatHours(decimal hours)
    {
        // Show whole hours without decimals; fractional with up to 2 places.
        return hours == Math.Floor(hours)
            ? hours.ToString("0", CultureInfo.InvariantCulture)
            : hours.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Renders the hours column as a "DaysxHours/Day=Total" formula when the
    /// line item has days × hours-per-day populated (v4 week-based lines).
    /// Falls back to just the total for legacy lines with no breakdown.
    /// </summary>
    private static string FormatHoursFormula(InvoiceLineItem item)
    {
        if (item.DaysWorked > 0 && item.HoursPerDay > 0)
        {
            return $"{FormatHours(item.DaysWorked)}x{FormatHours(item.HoursPerDay)}={FormatHours(item.Hours)}";
        }
        return FormatHours(item.Hours);
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

    private static string FormatLongDate(DateOnly date) =>
        date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);

}
