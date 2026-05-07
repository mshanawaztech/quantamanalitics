using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
