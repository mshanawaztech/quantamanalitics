using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using QuantamAnalytics.Api.Tenancy;
using QuantamAnalytics.Domain.Common;
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
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, currentTenant);

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
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, currentTenant);

        currentTenant.TenantId.Should().BeNull();
    }

    private sealed class TestCurrentTenant : ICurrentTenantSetter, ICurrentTenant
    {
        public Guid? TenantId { get; private set; }

        public void SetTenantId(Guid? tenantId)
        {
            TenantId = tenantId;
        }
    }
}
