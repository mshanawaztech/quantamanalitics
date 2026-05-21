namespace QuantamAnalytics.Domain.Pdf;

/// <summary>
/// Neutral branding fallbacks used when a tenant hasn't filled in their
/// branding yet. These are deliberately generic placeholders — NEVER real
/// names, emails, phone numbers, or bank/account details. Real values
/// belong in the per-tenant
/// <see cref="QuantamAnalytics.Domain.Entities.TenantBranding"/> row, set
/// via the Branding page, so sensitive financial/PII data lives in the
/// database (per tenant) and not in source control.
/// </summary>
/// <remarks>
/// Order of precedence for any branding field on the invoice PDF:
///   1. The tenant's <see cref="QuantamAnalytics.Domain.Entities.TenantBranding"/> value (if non-null)
///   2. <see cref="BrandingDefaults"/> below
///
/// Identity and bank fields default to empty strings: the PDF renderer
/// hides any remit line that is blank, so an unconfigured tenant simply
/// gets an invoice with no bank block rather than someone else's details.
/// </remarks>
public static class BrandingDefaults
{
    public const string DisplayName = "Your Company";
    public const string LegalName = "Your Company, LLC";
    public const string ContactEmail = "";
    public const string ContactPhone = "";

    public const string BankName = "";
    public const string BankAccountNumber = "";
    public const string BankRoutingNumber = "";

    public const decimal DefaultHourlyRate = 0m;
    public const string DefaultCurrency = "USD";
    public const int DefaultPaymentTermsDays = 14;

    public const string PrimaryColorHex = "#1a2d5a";
    public const string AccentColorHex = "#e6c9a8";
}
