using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Read access to the SOC 2 audit log. Gated on
/// <see cref="AuthorizationPolicies.RequirePlatformAdmin"/> — recruiters and
/// clients have no business reading raw mutation history. Tenant scoping
/// is the standard global query filter; PlatformAdmin acting on behalf of
/// a specific tenant sees that tenant's slice and nothing else.
/// </summary>
/// <remarks>
/// The endpoint deliberately offers narrow filters (entity type / id /
/// time range / page size) rather than a full SQL surface — auditors want
/// repeatable evidence pulls, not exploratory ad-hoc queries. Anything
/// richer should land in a separate "audit reports" feature with explicit
/// SOC 2 control mapping.
/// </remarks>
public static class AuditLogEndpoint
{
    public const int MaxPageSize = 200;

    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/audit-log")
            .WithTags("Admin · Audit Log")
            .RequireAuthorization(AuthorizationPolicies.RequirePlatformAdmin);

        group.MapGet("/", QueryAsync);

        return app;
    }

    private static async Task<Results<Ok<AuditLogQueryResponse>, ProblemHttpResult>> QueryAsync(
        AppDbContext db,
        ICurrentTenant currentTenant,
        string? authSubject,
        string? action,
        string? entityType,
        string? entityId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            return TypedResults.Problem(
                title: "Tenant assignment required",
                detail: "Audit-log queries require a tenant_id claim in the authenticated session — switch to the target tenant before reading its log.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var size = Math.Clamp(pageSize ?? 100, 1, MaxPageSize);

        var query = db.AuditLogEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(authSubject))
        {
            var trimmed = authSubject.Trim();
            query = query.Where(x => x.AuthSubject == trimmed);
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            if (!Enum.TryParse<AuditLogAction>(action.Trim(), ignoreCase: true, out var parsedAction))
            {
                return TypedResults.Problem(
                    title: "Unsupported audit action",
                    detail: "Use Created, Updated, or Deleted.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            query = query.Where(x => x.Action == parsedAction);
        }
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var trimmed = entityType.Trim();
            query = query.Where(x => x.EntityType == trimmed);
        }
        if (!string.IsNullOrWhiteSpace(entityId))
        {
            var trimmed = entityId.Trim();
            query = query.Where(x => x.EntityId == trimmed);
        }
        if (from is not null)
        {
            query = query.Where(x => x.RecordedAtUtc >= from.Value);
        }
        if (to is not null)
        {
            query = query.Where(x => x.RecordedAtUtc <= to.Value);
        }

        var rows = await query
            .OrderByDescending(x => x.RecordedAtUtc)
            .Take(size)
            .Select(x => new AuditLogEntryResponse(
                x.Id,
                x.AuthSubject,
                x.Action.ToString(),
                x.EntityType,
                x.EntityId,
                x.MetadataJson,
                x.RecordedAtUtc))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new AuditLogQueryResponse(rows, size));
    }
}

public sealed record AuditLogQueryResponse(
    AuditLogEntryResponse[] Items,
    int PageSize);

public sealed record AuditLogEntryResponse(
    Guid Id,
    string? AuthSubject,
    string Action,
    string EntityType,
    string EntityId,
    string? MetadataJson,
    DateTimeOffset RecordedAtUtc);
