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
    /// Inline brand mark — identical glyph to the SPA's qa-logo component
    /// (Q ring + upward analytics tick). Inlined as SVG so the PDF carries
    /// the mark without us shipping a binary asset; colors hard-coded
    /// white-on-accent so the mark reads against the navy banner.
    /// </summary>
    private const string BrandMarkSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">
          <circle cx="32" cy="32" r="26" fill="none" stroke="#ffffff" stroke-width="6"/>
          <line x1="48" y1="48" x2="58" y2="58" stroke="#ffffff" stroke-width="6" stroke-linecap="round"/>
          <polyline points="18,40 27,32 35,36 46,22" fill="none" stroke="#e6c9a8" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
          <circle cx="46" cy="22" r="3" fill="#e6c9a8"/>
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

            // Identity text — fall back to platform defaults (Quantam) so a
            // brand-new tenant gets a usable letterhead even before they
            // visit /settings/branding.
            var displayName = branding?.DisplayName ?? BrandingDefaults.DisplayName;
            var legalName = branding?.LegalName ?? BrandingDefaults.LegalName;
            var contactEmail = branding?.ContactEmail ?? BrandingDefaults.ContactEmail;
            // qa005 — per-invoice contact phone wins over branding's phone
            // so a contractor can route a specific invoice to a different
            // contact without rebranding their tenant.
            var contactPhone = invoice.RemitContactPhone
                ?? branding?.ContactPhone ?? BrandingDefaults.ContactPhone;

            layers.PrimaryLayer().PaddingLeft(40).PaddingTop(24).PaddingBottom(20).Row(row =>
            {
                // Brand mark on the left so the platform identity is
                // visible even when the tenant hasn't uploaded a logo.
                row.ConstantItem(56).Height(56).AlignTop().Svg(BrandMarkSvg);
                row.ConstantItem(16); // gutter

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(displayName).FontSize(20).Bold().FontColor(Colors.White);

                    if (!string.Equals(legalName, displayName, StringComparison.OrdinalIgnoreCase))
                    {
                        c.Item().PaddingTop(2).Text(legalName)
                            .FontSize(13).Bold().FontColor(Colors.White);
                    }

                    if (!string.IsNullOrWhiteSpace(contactEmail))
                    {
                        c.Item().PaddingTop(4).Text(contactEmail)
                            .FontSize(11).FontColor(Colors.White);
                    }
                    if (!string.IsNullOrWhiteSpace(contactPhone))
                    {
                        c.Item().PaddingTop(2).Text("Cell: " + contactPhone)
                            .FontSize(11).FontColor(Colors.White);
                    }

                    var address = FormatAddress(branding);
                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        c.Item().PaddingTop(4).Text(address!).FontSize(10).FontColor(Colors.White);
                    }
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

    private static string? FormatAddress(TenantBranding? b)
    {
        if (b is null) return null;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(b.AddressLine1)) parts.Add(b.AddressLine1!);
        if (!string.IsNullOrWhiteSpace(b.AddressLine2)) parts.Add(b.AddressLine2!);
        var locality = new[] { b.City, b.StateRegion, b.PostalCode }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (locality.Length > 0) parts.Add(string.Join(", ", locality));
        if (!string.IsNullOrWhiteSpace(b.Country)) parts.Add(b.Country!);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
