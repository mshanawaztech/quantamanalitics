using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Tests.TestAuth;

namespace QuantamAnalytics.Tests;

/// <summary>
/// Integration tests for /me. Two scenarios:
///   1. No Auth0 config → endpoint isn't routed, request returns 404
///      (Program.cs only calls MapMeEndpoint when AddPlatformAuth wires up).
///   2. Auth0 configured but no Bearer token → 401.
///
/// Real "valid token" tests live in PR-06.5 once the Angular client can
/// mint a token, or in a TestAuthHandler-based fixture if we add one.
/// </summary>
public sealed class MeEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MeEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Me_returns_404_when_Auth0_is_not_configured()
    {
        // Default WebApplicationFactory<Program> uses the Test environment;
        // Auth0:Domain / Auth0:Audience are unset so AddPlatformAuth returns
        // false and MapMeEndpoint is never called.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Me_returns_401_when_Auth0_is_configured_but_no_token_provided()
    {
        // Override config so AddPlatformAuth wires up real JWT validation.
        // We don't need Auth0 to be reachable — the bearer middleware fails
        // on missing-header BEFORE attempting JWKS discovery.
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth0:Domain", "fake-tenant.us.auth0.com");
            builder.UseSetting("Auth0:Audience", "https://api.quantamanalitics.com");
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync("/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_returns_roles_permissions_and_tenant_for_authenticated_user()
    {
        var tenantId = Guid.NewGuid();

        var client = _factory.WithAuthenticatedUser(
            tenantId,
            "auth0|hr-admin-1",
            "hradmin@example.com",
            Roles.HrAdmin,
            Roles.Interviewer)
            .CreateClient();

        var response = await client.GetAsync("/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<MeResponse>();

        payload.Should().NotBeNull();
        payload!.Sub.Should().Be("auth0|hr-admin-1");
        payload.Email.Should().Be("hradmin@example.com");
        payload.Roles.Should().BeEquivalentTo(new[] { Roles.HrAdmin, Roles.Interviewer });
        payload.Permissions.Should().BeEquivalentTo(new[]
        {
            PlatformPermissions.InterviewWorkspace,
            PlatformPermissions.RecruitingWorkspace,
        });
        payload.TenantId.Should().Be(tenantId.ToString());
    }
}
