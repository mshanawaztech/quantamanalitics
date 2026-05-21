using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuantamAnalytics.Api.Tenancy;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;
using QuantamAnalytics.Infrastructure.Tenancy;

namespace QuantamAnalytics.Tests;

public sealed class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task Middleware_sets_current_tenant_from_valid_claim()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(Roles.TenantIdClaim, tenantId.ToString())
            ], "test"))
        };

        var currentTenant = new TestCurrentTenant();
        var currentUser = new TestCurrentUser();
        var middleware = new TenantResolutionMiddleware(
            _ => Task.CompletedTask,
            NullLogger<TenantResolutionMiddleware>.Instance);
        // The tenant must exist in the DB — the middleware now verifies the
        // claimed tenant_id is real before trusting it.
        using var db = NewContext(tenantId);

        await middleware.InvokeAsync(httpContext, currentTenant, currentUser, db);

        currentTenant.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Middleware_drops_claim_when_tenant_does_not_exist()
    {
        // A well-formed tenant_id that no longer maps to a row — e.g. a JWT
        // minted against a previous database. The middleware must NOT trust
        // it, otherwise audit-logged writes fail the tenants foreign key.
        var staleTenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(Roles.TenantIdClaim, staleTenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, "auth0|orphan-1"),
            ], "test"))
        };

        var currentTenant = new TestCurrentTenant();
        var currentUser = new TestCurrentUser();
        var middleware = new TenantResolutionMiddleware(
            _ => Task.CompletedTask,
            NullLogger<TenantResolutionMiddleware>.Instance);
        // Empty DB: neither the claim nor any membership resolves a live tenant.
        using var db = NewContext();

        await middleware.InvokeAsync(httpContext, currentTenant, currentUser, db);

        currentTenant.TenantId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Middleware_leaves_current_tenant_empty_when_claim_is_missing_or_invalid(
        string? claimValue)
    {
        var claims = new List<Claim>();
        if (claimValue is not null)
        {
            claims.Add(new Claim(Roles.TenantIdClaim, claimValue));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };

        var currentTenant = new TestCurrentTenant();
        var currentUser = new TestCurrentUser();
        var middleware = new TenantResolutionMiddleware(
            _ => Task.CompletedTask,
            NullLogger<TenantResolutionMiddleware>.Instance);
        using var db = NewContext();

        await middleware.InvokeAsync(httpContext, currentTenant, currentUser, db);

        // No tenant claim AND no NameIdentifier (auth subject) was added,
        // so the membership-fallback branch in the middleware is skipped
        // and TenantId stays null. The DB is never opened in this path.
        currentTenant.TenantId.Should().BeNull();
    }

    /// <summary>
    /// Build an in-memory AppDbContext, optionally pre-seeded with tenants.
    /// The middleware now verifies the claimed tenant_id exists, so it issues
    /// a real query against this context — a non-opened Npgsql context would
    /// throw on connect. Each call gets an isolated in-memory store.
    /// </summary>
    private static AppDbContext NewContext(params Guid[] seededTenantIds)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenant-mw-{Guid.NewGuid():N}")
            .UseSnakeCaseNamingConvention()
            .Options;
        var db = new AppDbContext(options, new TestCurrentTenant());

        foreach (var id in seededTenantIds)
        {
            var tenant = new Tenant("t-" + id.ToString("N")[..8], "Test Tenant");
            db.Tenants.Add(tenant).Property(t => t.Id).CurrentValue = id;
        }
        if (seededTenantIds.Length > 0)
        {
            db.SaveChanges();
        }

        return db;
    }

    private sealed class TestCurrentTenant : ICurrentTenantSetter, ICurrentTenant
    {
        public Guid? TenantId { get; private set; }

        public void SetTenantId(Guid? tenantId)
        {
            TenantId = tenantId;
        }
    }

    private sealed class TestCurrentUser : ICurrentUserSetter, ICurrentUser
    {
        public string? AuthSubject { get; private set; }

        public void SetAuthSubject(string? authSubject)
        {
            AuthSubject = authSubject;
        }
    }
}
