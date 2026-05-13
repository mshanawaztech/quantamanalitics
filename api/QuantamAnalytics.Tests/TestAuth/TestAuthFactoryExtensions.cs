using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuantamAnalytics.Domain.Common;

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
