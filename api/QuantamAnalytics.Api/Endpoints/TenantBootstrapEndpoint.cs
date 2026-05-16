using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Self-service bootstrap path for authenticated users whose JWT does not
/// carry a <c>tenant_id</c> custom claim yet — e.g. a brand-new account
/// signing in before the Auth0 post-login Action has been configured for
/// them, or the maintainer poking around their own deployment.
///
/// Without this endpoint every tenant-scoped portal returns 412 and the
/// user is locked out with no in-app recovery.
/// </summary>
public static class TenantBootstrapEndpoint
{
    private const string PostgresUniqueViolation = "23505";

    public static IEndpointRouteBuilder MapTenantBootstrapEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/me/tenant")
            .WithTags("Tenant bootstrap")
            .RequireAuthorization();

        group.MapGet("/", GetMembershipAsync);
        group.MapPost("/join-demo", JoinDemoAsync);

        return app;
    }

    private static async Task<Results<Ok<TenantMembershipResponse>, NotFound, ProblemHttpResult>> GetMembershipAsync(
        AppDbContext db,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return SubjectRequired();
        }

        var subject = currentUser.AuthSubject;
        var row = await db.TenantMemberships
            .AsNoTracking()
            .Where(m => m.AuthSubject == subject)
            .Join(
                db.Tenants.IgnoreQueryFilters(),
                m => m.TenantId,
                t => t.Id,
                (m, t) => new TenantMembershipResponse(t.Id, t.Slug, t.Name, m.JoinedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(row);
    }

    private static async Task<Results<Ok<TenantMembershipResponse>, ProblemHttpResult>> JoinDemoAsync(
        AppDbContext db,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.AuthSubject))
        {
            return SubjectRequired();
        }

        var subject = currentUser.AuthSubject;

        // Already a member somewhere? Bail with the existing row — joining
        // is idempotent so a "Join demo" button can't accidentally fork
        // the user across two tenants.
        var existing = await db.TenantMemberships
            .AsNoTracking()
            .Where(m => m.AuthSubject == subject)
            .Join(
                db.Tenants.IgnoreQueryFilters(),
                m => m.TenantId,
                t => t.Id,
                (m, t) => new TenantMembershipResponse(t.Id, t.Slug, t.Name, m.JoinedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return TypedResults.Ok(existing);
        }

        // Pick the seeded demo tenant if one exists, otherwise mint a new
        // "Demo" tenant. IgnoreQueryFilters because the user has no tenant
        // context yet.
        var demoTenant = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.IsActive)
            .OrderBy(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (demoTenant is null)
        {
            demoTenant = new Tenant("demo", "Demo staffing firm");
            db.Tenants.Add(demoTenant);
            await db.SaveChangesAsync(cancellationToken);
        }

        var membership = new TenantMembership(subject, demoTenant.Id);
        db.TenantMemberships.Add(membership);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
                                            && pg.SqlState == PostgresUniqueViolation)
        {
            // Race: another request claimed the membership between the
            // existence check and the insert. Re-read and return that row.
            var raced = await db.TenantMemberships
                .AsNoTracking()
                .Where(m => m.AuthSubject == subject)
                .Join(
                    db.Tenants.IgnoreQueryFilters(),
                    m => m.TenantId,
                    t => t.Id,
                    (m, t) => new TenantMembershipResponse(t.Id, t.Slug, t.Name, m.JoinedAtUtc))
                .FirstAsync(cancellationToken);
            return TypedResults.Ok(raced);
        }

        return TypedResults.Ok(new TenantMembershipResponse(
            demoTenant.Id,
            demoTenant.Slug,
            demoTenant.Name,
            membership.JoinedAtUtc));
    }

    private static ProblemHttpResult SubjectRequired() =>
        TypedResults.Problem(
            title: "Authenticated subject required",
            detail: "Tenant bootstrap requires an authenticated user. Sign in first.",
            statusCode: StatusCodes.Status401Unauthorized);
}

public sealed record TenantMembershipResponse(
    Guid TenantId,
    string Slug,
    string Name,
    DateTimeOffset JoinedAtUtc);
