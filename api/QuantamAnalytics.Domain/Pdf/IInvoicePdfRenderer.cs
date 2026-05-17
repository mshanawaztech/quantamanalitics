using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Domain.Pdf;

/// <summary>
/// Renders a single invoice — with line items, tenant branding, and bank
/// remit-to details — into a PDF byte stream. Implementation lives in the
/// Infrastructure layer; Endpoints depend only on this abstraction so the
/// rendering library can be swapped without touching the API surface.
/// </summary>
public interface IInvoicePdfRenderer
{
    /// <summary>
    /// Render the invoice as a PDF. Caller owns the returned byte array.
    /// </summary>
    /// <param name="invoice">The invoice to render. <c>LineItems</c> must be loaded.</param>
    /// <param name="branding">Tenant branding to apply. Pass null to render with defaults.</param>
    /// <param name="logoBytes">Optional logo image bytes (PNG / JPG). Passed through to the renderer.</param>
    byte[] Render(Invoice invoice, TenantBranding? branding, byte[]? logoBytes);
}
