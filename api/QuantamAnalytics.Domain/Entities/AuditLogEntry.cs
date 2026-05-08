using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Domain.Entities;

/// <summary>
/// Append-only record of a mutation against tenant-scoped data. One row per
/// EF Core EntityEntry that hits <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChanges()"/>
/// in Added / Modified / Deleted state. Captured automatically by the
/// <c>AuditLogSaveChangesInterceptor</c> — application code never writes
/// these directly.
/// </summary>
/// <remarks>
/// Persistence model: tenant-scoped (so the global query filter applies and
/// a recruiter at tenant A can never query tenant B's audit trail), keyed
/// by Guid v7 so rows sort chronologically by primary key. The
/// <c>RecordedAtUtc</c> column is also indexed in descending order to make
/// "most recent first" listings cheap.
///
/// Why a separate entity instead of an event-sourcing log: this is the
/// SOC 2 audit-evidence surface, not a domain event bus. Keep it boring —
/// one row per change, pre-computed columns we know auditors will want
/// (subject, entity type, entity id, action), and a free-form metadata
/// JSON column for everything else.
/// </remarks>
public sealed class AuditLogEntry : ITenantScoped
{
    private AuditLogEntry() { }

    public AuditLogEntry(
        Guid tenantId,
        string? authSubject,
        AuditLogAction action,
        string entityType,
        string entityId,
        string? metadataJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        AuthSubject = string.IsNullOrWhiteSpace(authSubject) ? null : authSubject.Trim();
        Action = action;
        EntityType = entityType.Trim();
        EntityId = entityId.Trim();
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson;
        RecordedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Auth0 subject (<c>sub</c> claim) of the user who triggered the
    /// change. <c>null</c> when the change came from a system path with no
    /// HTTP context (background jobs, demo seeder, migration backfills).
    /// </summary>
    public string? AuthSubject { get; private set; }

    public AuditLogAction Action { get; private set; }

    /// <summary>CLR type name, e.g. <c>Application</c>, <c>Submission</c>.</summary>
    public string EntityType { get; private set; } = default!;

    /// <summary>Primary-key value as a string (Guid.ToString("N")).</summary>
    public string EntityId { get; private set; } = default!;

    /// <summary>
    /// Free-form context — change set summary, request id, anything the
    /// interceptor wants to surface. Stored as <c>jsonb</c> so the SOC 2
    /// query path can index into it without parsing strings.
    /// </summary>
    public string? MetadataJson { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }
}

public enum AuditLogAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
}
