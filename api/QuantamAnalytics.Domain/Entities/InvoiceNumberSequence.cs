using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Per-tenant, per-year counter used to mint human-readable invoice numbers
/// in the form <c>INV-{year}-{4-digit sequence}</c> (e.g. INV-2026-0001).
/// </summary>
/// <remarks>
/// Stored in its own table rather than a database SEQUENCE so multi-tenant
/// isolation comes for free: every tenant has its own row per year and they
/// cannot see each other's counters. The endpoint bumps <see cref="NextValue"/>
/// inside the same transaction that inserts the invoice, so two concurrent
/// requests cannot mint the same number (Postgres row lock on UPDATE).
///
/// Uses a surrogate <see cref="Id"/> primary key (UUID v7) to satisfy
/// <see cref="ITenantScoped"/> — the natural <c>(TenantId, Year)</c> tuple
/// becomes a unique index in the EF configuration so the counter is still
/// one-row-per-tenant-per-year.
/// </remarks>
public sealed class InvoiceNumberSequence : ITenantScoped
{
    private InvoiceNumberSequence() { }

    public InvoiceNumberSequence(Guid tenantId, int year)
    {
        if (year < 2000 || year > 9999)
        {
            throw new ArgumentException("Year must be a 4-digit value between 2000 and 9999.", nameof(year));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Year = year;
        NextValue = 1;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public int Year { get; private set; }

    /// <summary>The next sequence number to mint. Starts at 1; monotonically increases.</summary>
    public int NextValue { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Bumps the counter and returns the formatted invoice number (e.g. <c>INV-2026-0001</c>).
    /// Callers MUST persist the entity inside the same transaction that inserts
    /// the invoice — the row lock acquired here serializes concurrent inserts.
    /// </summary>
    public string MintNext()
    {
        var minted = NextValue;
        NextValue++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return $"INV-{Year}-{minted:D4}";
    }
}
