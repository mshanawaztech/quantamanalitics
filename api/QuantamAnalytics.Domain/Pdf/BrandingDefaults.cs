namespace QuantamAnalytics.Domain.Pdf;

/// <summary>
/// Platform-owner branding defaults used as a fallback when a tenant
/// hasn't filled in /settings/branding yet. Today these are
/// Quantamanalytics's own values — for the platform-owner tenant they
/// just work without any setup. Once we onboard real SaaS customers
/// we'll move these into per-tenant config so each org sees their own
/// defaults on first paint.
/// </summary>
/// <remarks>
/// Order of precedence for any branding field on the invoice PDF:
///   1. The tenant's <see cref="QuantamAnalytics.Domain.Entities.TenantBranding"/> value (if non-null)
///   2. <see cref="BrandingDefaults"/> below (always populated)
///
/// This means a brand-new tenant who's never visited /settings/branding
/// still produces a usable invoice; the first time they edit the form
/// they see today's defaults pre-populated so they know exactly what to
/// override.
/// </remarks>
public static class BrandingDefaults
{
    public const string DisplayName = "Mohammed Khan";
    public const string LegalName = "Quantamanalytics LLC";
    public const string ContactEmail = "mohammed.khan@quantamanalytics.com";
    public const string ContactPhone = "909-560-3095";

    public const string BankName = "Chase";
    public const string BankAccountNumber = "993681185";
    public const string BankRoutingNumber = "021202337";

    public const decimal DefaultHourlyRate = 55m;
    public const string DefaultCurrency = "USD";
    public const int DefaultPaymentTermsDays = 14;

    public const string PrimaryColorHex = "#1a2d5a";
    public const string AccentColorHex = "#e6c9a8";
}
