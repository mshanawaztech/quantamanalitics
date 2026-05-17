using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Per-tenant branding and remit-to details surfaced on every invoice PDF
/// the tenant produces. One row per tenant — exists for every Tenant via
/// either the migration backfill or the new-tenant signup flow. Fields are
/// all optional at the domain layer so a brand-new tenant has a reasonable
/// experience even before they fill the form in /settings/branding.
/// </summary>
/// <remarks>
/// Why a separate entity instead of fields on <see cref="Tenant"/>:
/// - The branding form gets edited frequently; the core Tenant row should
///   stay write-light to keep audit-log volume manageable.
/// - Some tenants will never customize branding — keeping defaults in a
///   nullable sibling row avoids dragging unused columns through every
///   Tenant query.
/// - A future "white-label per region" or "per-business-unit branding"
///   feature can extend this entity without touching Tenant.
/// </remarks>
public sealed class TenantBranding : ITenantScoped
{
    private TenantBranding() { }

    public TenantBranding(Guid tenantId)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    // ── Letterhead identity ──────────────────────────────────────────
    /// <summary>Display name on the invoice (overrides <see cref="Tenant.Name"/> when set).</summary>
    public string? DisplayName { get; private set; }

    /// <summary>Full legal entity (e.g., "Quantamanalytics LLC"). Rendered under the display name.</summary>
    public string? LegalName { get; private set; }

    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }

    // ── Postal address (used on invoices, can be empty) ──────────────
    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? City { get; private set; }
    public string? StateRegion { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Country { get; private set; }

    // ── Bank / ACH remit-to (rendered on the invoice PDF) ────────────
    public string? BankName { get; private set; }
    public string? BankAccountNumber { get; private set; }
    public string? BankRoutingNumber { get; private set; }

    // ── Defaults pre-populated on every new invoice ──────────────────
    public decimal? DefaultHourlyRate { get; private set; }
    public string? DefaultCurrency { get; private set; }
    /// <summary>How many days from issue date the invoice is due by default.</summary>
    public int? DefaultPaymentTermsDays { get; private set; }

    // ── Visual identity ──────────────────────────────────────────────
    /// <summary>Hex color (e.g., #1a2d5a) used for the banner and accents.</summary>
    public string? PrimaryColorHex { get; private set; }

    /// <summary>Hex color (e.g., #e6c9a8) used for the table header and divider.</summary>
    public string? AccentColorHex { get; private set; }

    /// <summary>R2 object key for the logo. Renderer fetches & embeds inline.</summary>
    public string? LogoObjectKey { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Replace all editable fields in one shot. Trims whitespace and
    /// normalizes hex colors to lowercase. Validates currency / rate /
    /// terms domain rules.
    /// </summary>
    public void UpdateProfile(
        string? displayName,
        string? legalName,
        string? contactEmail,
        string? contactPhone,
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? stateRegion,
        string? postalCode,
        string? country,
        string? bankName,
        string? bankAccountNumber,
        string? bankRoutingNumber,
        decimal? defaultHourlyRate,
        string? defaultCurrency,
        int? defaultPaymentTermsDays,
        string? primaryColorHex,
        string? accentColorHex)
    {
        if (defaultHourlyRate is < 0)
        {
            throw new ArgumentException("Default hourly rate must be non-negative.", nameof(defaultHourlyRate));
        }
        if (defaultPaymentTermsDays is < 0 or > 365)
        {
            throw new ArgumentException("Default payment terms must be between 0 and 365 days.", nameof(defaultPaymentTermsDays));
        }
        if (defaultCurrency is not null && defaultCurrency.Trim().Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(defaultCurrency));
        }

        DisplayName = Trim(displayName);
        LegalName = Trim(legalName);
        ContactEmail = Trim(contactEmail)?.ToLowerInvariant();
        ContactPhone = Trim(contactPhone);
        AddressLine1 = Trim(addressLine1);
        AddressLine2 = Trim(addressLine2);
        City = Trim(city);
        StateRegion = Trim(stateRegion);
        PostalCode = Trim(postalCode);
        Country = Trim(country);
        BankName = Trim(bankName);
        BankAccountNumber = Trim(bankAccountNumber);
        BankRoutingNumber = Trim(bankRoutingNumber);
        DefaultHourlyRate = defaultHourlyRate;
        DefaultCurrency = Trim(defaultCurrency)?.ToUpperInvariant();
        DefaultPaymentTermsDays = defaultPaymentTermsDays;
        PrimaryColorHex = NormalizeHex(primaryColorHex);
        AccentColorHex = NormalizeHex(accentColorHex);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Stamp the R2 object key after a successful logo upload.</summary>
    public void AttachLogo(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        LogoObjectKey = objectKey.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeHex(string? value)
    {
        var trimmed = Trim(value);
        if (trimmed is null) return null;
        if (!trimmed.StartsWith('#')) trimmed = "#" + trimmed;
        return trimmed.ToLowerInvariant();
    }
}
