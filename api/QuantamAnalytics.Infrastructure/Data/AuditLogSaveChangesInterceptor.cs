using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Infrastructure.Data;

/// <summary>
/// Hooks into <see cref="DbContext.SaveChangesAsync(System.Threading.CancellationToken)"/>
/// (and its sync sibling) to capture every Added / Modified / Deleted
/// <see cref="ITenantScoped"/> entity as one row in
/// <see cref="AppDbContext.AuditLogEntries"/>. The audit rows participate
/// in the same transaction as the source change — either both land or
/// neither does, so the audit trail can never disagree with the data.
/// </summary>
/// <remarks>
/// What gets captured:
/// - Every <see cref="ITenantScoped"/> entity in Added / Modified /
///   Deleted state at SaveChanges time.
/// - The current tenant id (from <see cref="ICurrentTenant"/>) and the
///   acting auth subject (from <see cref="IHttpContextAccessor"/>).
/// - For Modified entries, a JSON object of changed property names →
///   from/to values. Original/current values are flattened to strings
///   to keep the column type simple (jsonb of strings).
///
/// What gets skipped:
/// - The audit entries themselves (otherwise every audit insert would
///   recursively log itself).
/// - Non-tenant-scoped entities (the <see cref="Tenant"/> aggregate
///   itself — tenant rows are platform-admin-only and tracked elsewhere).
/// - Changes made when no current tenant is resolved (background jobs
///   that explicitly haven't entered a tenant scope). These are rare and
///   should not generate orphan audit rows.
/// </remarks>
public sealed class AuditLogSaveChangesInterceptor : SaveChangesInterceptor
{
    // Lazy probe of whether the audit_log_entries table exists in the
    // target database. Process-wide cache (interceptor is scoped, but the
    // table state is global). Three values: 0 = unknown, 1 = available,
    // 2 = unavailable. Volatile reads are enough — racing two probes is
    // harmless, both will return the same answer.
    private static int _auditTableState;

    private const int StateUnknown = 0;
    private const int StateAvailable = 1;
    private const int StateUnavailable = 2;

    /// <summary>
    /// Postgres SQLSTATE for "undefined table" — what the server returns
    /// when a SELECT references a relation that doesn't exist. Treat this
    /// specifically; any other DB error should propagate.
    /// </summary>
    private const string PostgresUndefinedTableSqlState = "42P01";

    private readonly ICurrentTenant _currentTenant;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogSaveChangesInterceptor(
        ICurrentTenant currentTenant,
        IHttpContextAccessor httpContextAccessor)
    {
        _currentTenant = currentTenant;
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AppDbContext context)
        {
            CaptureAuditEntries(context);
        }
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is AppDbContext context)
        {
            CaptureAuditEntries(context);
        }
        return result;
    }

    private void CaptureAuditEntries(AppDbContext context)
    {
        if (_currentTenant.TenantId is null)
        {
            // System paths with no tenant scope (background jobs, demo
            // seeder running outside an HTTP request) — skip rather than
            // emit orphan rows. If those paths grow we'll need an
            // explicit "platform" subject convention here.
            return;
        }

        if (!IsAuditTableAvailable(context))
        {
            // Migration hasn't been applied yet. Don't insert audit rows
            // we know will fail — the original SaveChanges would otherwise
            // roll back, taking the source mutation with it. Audit
            // logging gracefully degrades to off until the table exists.
            return;
        }

        var tenantId = _currentTenant.TenantId.Value;
        var subject = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        // Snapshot the candidate entries first — Add() during enumeration
        // would otherwise pull our own audit rows back into the loop.
        var candidates = context.ChangeTracker.Entries()
            .Where(e =>
                e.Entity is ITenantScoped &&
                e.Entity is not AuditLogEntry &&
                (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .ToArray();

        foreach (var entry in candidates)
        {
            var action = entry.State switch
            {
                EntityState.Added => AuditLogAction.Created,
                EntityState.Modified => AuditLogAction.Updated,
                EntityState.Deleted => AuditLogAction.Deleted,
                _ => (AuditLogAction?)null,
            };
            if (action is null)
            {
                continue;
            }

            var entityType = entry.Metadata.ClrType.Name;
            var entityId = ResolvePrimaryKey(entry);

            context.AuditLogEntries.Add(new AuditLogEntry(
                tenantId: tenantId,
                authSubject: subject,
                action: action.Value,
                entityType: entityType,
                entityId: entityId,
                metadataJson: BuildMetadataJson(entry, action.Value)));
        }
    }

    /// <summary>
    /// Probes the database once per process to confirm the
    /// <c>audit_log_entries</c> table exists. Caches the result so every
    /// subsequent SaveChanges hits the cache, not the network.
    /// </summary>
    /// <remarks>
    /// Why probe at all: in the window between a deploy that ships
    /// <see cref="AuditLogEntry"/> code and the migration that creates
    /// the table, every tenant-scoped write would otherwise fail because
    /// the interceptor's INSERT references a table that doesn't exist.
    /// Better to silently skip audit capture (with a one-time warning
    /// log) than to take all writes down with us.
    ///
    /// Once <c>AuditLogBaseline</c> is applied to the target database,
    /// the probe sees the table on its first call and audit capture
    /// switches on for the rest of the process lifetime.
    /// </remarks>
    private bool IsAuditTableAvailable(AppDbContext context)
    {
        var current = Volatile.Read(ref _auditTableState);
        if (current == StateAvailable)
        {
            return true;
        }
        if (current == StateUnavailable)
        {
            return false;
        }

        try
        {
            // LIMIT 0 = no row work, just a parse + plan. Cheap probe.
            context.Database.ExecuteSqlRaw("SELECT 1 FROM audit_log_entries LIMIT 0");
            Volatile.Write(ref _auditTableState, StateAvailable);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresUndefinedTableSqlState)
        {
            Volatile.Write(ref _auditTableState, StateUnavailable);
            _logger.LogWarning(
                "audit_log_entries table not found — audit logging is disabled. " +
                "Apply the AuditLogBaseline migration to enable it.");
            return false;
        }
        catch (Exception ex)
        {
            // Any other error (connection blip, permissions) — leave the
            // state unknown so the next call retries. Don't poison-pill
            // the process on a transient failure.
            _logger.LogWarning(
                ex,
                "Audit log availability probe failed; will retry on the next SaveChanges.");
            return false;
        }
    }

    /// <summary>
    /// Returns the primary-key value of <paramref name="entry"/> as a
    /// string. Composite keys are joined with <c>"|"</c>. Falls back to
    /// the empty string only if the entity has no key, which our schema
    /// never produces.
    /// </summary>
    private static string ResolvePrimaryKey(EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk is null)
        {
            return string.Empty;
        }

        var values = pk.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty);

        return string.Join("|", values);
    }

    /// <summary>
    /// For Modified entries, emits a small JSON object of changed
    /// property names → from/to string values. For Created and Deleted,
    /// returns <c>null</c> — the entity_id + action carry enough signal
    /// on their own and dumping the full entity bloats the column.
    /// </summary>
    private static string? BuildMetadataJson(EntityEntry entry, AuditLogAction action)
    {
        if (action != AuditLogAction.Updated)
        {
            return null;
        }

        var changes = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties.Where(p => p.IsModified))
        {
            changes[prop.Metadata.Name] = new
            {
                from = prop.OriginalValue?.ToString(),
                to = prop.CurrentValue?.ToString(),
            };
        }

        return changes.Count == 0 ? null : JsonSerializer.Serialize(changes);
    }
}
