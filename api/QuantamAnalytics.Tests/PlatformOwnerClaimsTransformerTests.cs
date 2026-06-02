using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using QuantamAnalytics.Api.Auth;
using QuantamAnalytics.Domain.Common;

namespace QuantamAnalytics.Tests;

public sealed class PlatformOwnerClaimsTransformerTests
{
    [Fact]
    public async Task Allowlisted_email_gets_platform_admin_role()
    {
        var transformer = Build("owner@example.com");
        var principal = NewPrincipal(email: "OWNER@example.com");

        var result = await transformer.TransformAsync(principal);

        result.IsInRole(Roles.PlatformAdmin).Should().BeTrue();
    }

    [Fact]
    public async Task Non_allowlisted_email_stays_unchanged()
    {
        var transformer = Build("owner@example.com");
        var principal = NewPrincipal(email: "intern@example.com");

        var result = await transformer.TransformAsync(principal);

        result.IsInRole(Roles.PlatformAdmin).Should().BeFalse();
    }

    [Fact]
    public async Task Empty_allowlist_is_no_op()
    {
        var transformer = Build("");
        var principal = NewPrincipal(email: "owner@example.com");

        var result = await transformer.TransformAsync(principal);

        result.IsInRole(Roles.PlatformAdmin).Should().BeFalse();
    }

    [Fact]
    public async Task Comma_separated_allowlist_supports_multiple_owners()
    {
        var transformer = Build("alice@quantam.com, bob@quantam.com");
        var alice = NewPrincipal(email: "alice@quantam.com");
        var bob = NewPrincipal(email: "BOB@quantam.com");

        (await transformer.TransformAsync(alice)).IsInRole(Roles.PlatformAdmin).Should().BeTrue();
        (await transformer.TransformAsync(bob)).IsInRole(Roles.PlatformAdmin).Should().BeTrue();
    }

    [Fact]
    public async Task Already_platform_admin_does_not_duplicate_claim()
    {
        var transformer = Build("owner@example.com");
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Email, "owner@example.com"),
                new Claim(Roles.RolesClaim, Roles.PlatformAdmin),
            ],
            authenticationType: "test",
            nameType: ClaimTypes.NameIdentifier,
            roleType: Roles.RolesClaim);
        var principal = new ClaimsPrincipal(identity);

        var result = await transformer.TransformAsync(principal);

        result.FindAll(Roles.RolesClaim)
            .Count(c => c.Value == Roles.PlatformAdmin)
            .Should().Be(1);
    }

    private static PlatformOwnerClaimsTransformer Build(string allowlist)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PlatformOwnerClaimsTransformer.ConfigKey] = allowlist,
            })
            .Build();
        return new PlatformOwnerClaimsTransformer(config);
    }

    private static ClaimsPrincipal NewPrincipal(string email)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, email)],
            authenticationType: "test",
            nameType: ClaimTypes.NameIdentifier,
            roleType: Roles.RolesClaim);
        return new ClaimsPrincipal(identity);
    }
}
