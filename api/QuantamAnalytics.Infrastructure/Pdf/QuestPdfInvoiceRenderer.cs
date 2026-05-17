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

    public QuestPdfInvoiceRenderer()
    {
        // QuestPDF community license — set once at process start. Idempotent.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(Invoice invoice, TenantBranding? branding, byte[]? logoBytes)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var primary = branding?.PrimaryColorHex ?? DefaultPrimaryHex;
        var accent = branding?.AccentColorHex ?? DefaultAccentHex;

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
                        body.Item().PaddingTop(16).Element(c => RenderBankBlock(c, branding));
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
    private static void RenderBanner(
        IContainer container,
        Invoice invoice,
        TenantBranding? branding,
        byte[]? logoBytes,
        string primary,
        string accent)
    {
        // The "angled" look in the reference is faked with two stacked
        // rectangles and a thin accent stripe between them. Cheaper than a
        // SVG clip path and looks identical at letter size.
        container.Height(140).Layers(layers =>
        {
            // Full-width navy background
            layers.Layer().Background(primary);
            // Accent stripe across the bottom
            layers.Layer().AlignBottom().Height(6).Background(accent);

            // Identity text
            layers.PrimaryLayer().PaddingLeft(40).PaddingTop(28).Column(c =>
            {
                c.Item().Text(branding?.DisplayName ?? branding?.LegalName ?? "Your Company")
                    .FontSize(20).Bold().FontColor(Colors.White);

                if (!string.IsNullOrWhiteSpace(branding?.LegalName) &&
                    !string.Equals(branding.LegalName, branding.DisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    c.Item().PaddingTop(2).Text(branding.LegalName)
                        .FontSize(13).Bold().FontColor(Colors.White);
                }

                if (!string.IsNullOrWhiteSpace(branding?.ContactEmail))
                {
                    c.Item().PaddingTop(4).Text(branding.ContactEmail!)
                        .FontSize(11).FontColor(Colors.White);
                }
                if (!string.IsNullOrWhiteSpace(branding?.ContactPhone))
                {
                    c.Item().PaddingTop(2).Text("Cell: " + branding.ContactPhone!)
                        .FontSize(11).FontColor(Colors.White);
                }

                var address = FormatAddress(branding);
                if (!string.IsNullOrWhiteSpace(address))
                {
                    c.Item().PaddingTop(4).Text(address!).FontSize(10).FontColor(Colors.White);
                }
            });

            // Optional logo, top-right
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

    private static void RenderBankBlock(IContainer container, TenantBranding? branding)
    {
        if (branding is null ||
            string.IsNullOrWhiteSpace(branding.BankName) &&
            string.IsNullOrWhiteSpace(branding.BankAccountNumber) &&
            string.IsNullOrWhiteSpace(branding.BankRoutingNumber))
        {
            return; // No bank details configured — skip the block entirely.
        }

        container.Column(col =>
        {
            col.Item().Text("Bank Details").FontSize(13).Bold();

            if (!string.IsNullOrWhiteSpace(branding.BankName))
            {
                col.Item().PaddingTop(6).Text("Bank Name: " + branding.BankName).FontSize(11).Bold();
            }
            if (!string.IsNullOrWhiteSpace(branding.LegalName))
            {
                col.Item().PaddingTop(2).Text("Name: " + branding.LegalName).FontSize(11).Bold();
            }
            if (!string.IsNullOrWhiteSpace(branding.BankAccountNumber))
            {
                col.Item().PaddingTop(2).Text("Account number").FontSize(11).Bold();
                col.Item().Text(branding.BankAccountNumber).FontSize(11).Bold();
            }
            if (!string.IsNullOrWhiteSpace(branding.BankRoutingNumber))
            {
                col.Item().PaddingTop(2).Text("Routing number").FontSize(11).Bold();
                col.Item().Text(branding.BankRoutingNumber).FontSize(11).Bold();
            }
        });
    }

    private static void RenderRatePeriodStrip(IContainer container, Invoice invoice, TenantBranding? branding, string primary)
    {
        var hourlyRate = branding?.DefaultHourlyRate
            ?? (invoice.LineItems.Count > 0 ? invoice.LineItems[0].Rate : 0m);

        container.Column(col =>
        {
            col.Item().Text(t =>
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

            table.Header(h =>
            {
                CellHeader(h, "Client Name", accent);
                CellHeader(h, "Hrs.\nWorked", accent, TextAlignment.Center);
                CellHeader(h, "Rate", accent, TextAlignment.Center);
                CellHeader(h, "TOTAL", accent, TextAlignment.Center);
            });

            foreach (var item in invoice.LineItems)
            {
                CellBody(table.Cell(), item.Description);
                CellBody(table.Cell(), FormatHours(item.Hours), TextAlignment.Center);
                CellBody(table.Cell(), FormatCurrency(invoice.Currency, item.Rate), TextAlignment.Center);
                CellBody(table.Cell(), FormatCurrency(invoice.Currency, item.Amount), TextAlignment.Center);
            }
        });
    }

    private static void CellHeader(
        TableDescriptor.TableHeaderDescriptor header,
        string text,
        string accent,
        TextAlignment alignment = TextAlignment.Left)
    {
        var cell = header.Cell().Background(accent).Padding(8);
        if (alignment == TextAlignment.Center)
        {
            cell.AlignCenter().Text(text).Bold().FontSize(11);
        }
        else
        {
            cell.Text(text).Bold().FontSize(11);
        }
    }

    private static void CellBody(IContainer cell, string text, TextAlignment alignment = TextAlignment.Left)
    {
        var styled = cell.BorderBottom(0.5f).BorderColor("#e6e8ee").Padding(8);
        if (alignment == TextAlignment.Center)
        {
            styled.AlignCenter().Text(text).FontSize(10);
        }
        else
        {
            styled.Text(text).FontSize(10);
        }
    }

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
