using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuantamAnalytics.Api.Tenancy;
using QuantamAnalytics.Domain.Common;
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
        using var db = NewContext();

        await middleware.InvokeAsync(httpContext, currentTenant, currentUser, db);

        currentTenant.TenantId.Should().Be(tenantId);
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
    /// Build a non-opened AppDbContext for tests that need to pass it
    /// through but never actually query. The middleware only touches the
    /// DB when both (a) no tenant_id claim is present, and (b) an
    /// authenticated subject exists. Neither test below trips (b).
    /// </summary>
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=qa_dev_test;Username=u;Password=p")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, new TestCurrentTenant());
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
