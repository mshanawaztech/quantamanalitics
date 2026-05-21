using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;

namespace QuantamAnalytics.Tests.TestAuth;

public static class TestAuthFactoryExtensions
{
    public static WebApplicationFactory<Program> WithAuthenticatedUser(
        this WebApplicationFactory<Program> factory,
        Guid tenantId,
        string authSubject,
        string email,
        params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authSubject),
            new(ClaimTypes.Email, email),
            new("email", email),
            new(Roles.TenantIdClaim, tenantId.ToString()),
            new("name", "Candidate Tester"),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(Roles.RolesClaim, role));
        }

        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth0:Domain", "fake-tenant.us.auth0.com");
            builder.UseSetting("Auth0:Audience", "https://api.quantamanalitics.com");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TestAuthClaimsProvider>();
                services.AddSingleton(new TestAuthClaimsProvider(claims));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

                // TenantResolutionMiddleware now verifies the JWT tenant_id
                // actually exists before trusting it (so a stale claim from a
                // migrated DB can't poison audit-logged writes). Bare-factory
                // tests authenticate against a random tenant id with no row
                // behind it, so seed that tenant on the in-memory provider.
                // Postgres-backed isolated tests seed their own real tenants
                // and use a relational provider, so they're skipped here.
                services.AddHostedService(sp => new SeedClaimedTenantHostedService(sp, tenantId));
            });
        });
    }

    public static WebApplicationFactory<Program> WithAuthenticatedUserWithoutTenant(
        this WebApplicationFactory<Program> factory,
        string authSubject,
        string email,
        params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authSubject),
            new(ClaimTypes.Email, email),
            new("email", email),
            new("name", "Candidate Tester"),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(Roles.RolesClaim, role));
        }

        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth0:Domain", "fake-tenant.us.auth0.com");
            builder.UseSetting("Auth0:Audience", "https://api.quantamanalitics.com");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TestAuthClaimsProvider>();
                services.AddSingleton(new TestAuthClaimsProvider(claims));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });
            });
        });
    }
}

/// <summary>
/// Ensures the authenticated user's claimed tenant exists when the host runs
/// on the EF in-memory provider (the no-connection-string fallback used by
/// bare-factory tests). Real Postgres-backed isolated tests seed their own
/// tenants and are skipped, so this can't collide with their fixtures.
/// </summary>
internal sealed class SeedClaimedTenantHostedService : IHostedService
{
    private const string InMemoryProvider = "Microsoft.EntityFrameworkCore.InMemory";

    private readonly IServiceProvider _services;
    private readonly Guid _tenantId;

    public SeedClaimedTenantHostedService(IServiceProvider services, Guid tenantId)
    {
        _services = services;
        _tenantId = tenantId;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Only meaningful for the in-memory fallback; relational providers
        // belong to isolated tests that manage their own tenant rows.
        if (db.Database.ProviderName != InMemoryProvider)
        {
            return;
        }

        var exists = await db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == _tenantId, cancellationToken);
        if (exists)
        {
            return;
        }

        var tenant = new Tenant("t-" + _tenantId.ToString("N")[..8], "Test Tenant");
        var entry = db.Tenants.Add(tenant);
        entry.Property(t => t.Id).CurrentValue = _tenantId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
