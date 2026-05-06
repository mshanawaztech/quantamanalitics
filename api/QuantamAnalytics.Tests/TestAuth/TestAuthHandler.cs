using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuantamAnalytics.Tests.TestAuth;

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    private readonly TestAuthClaimsProvider _claimsProvider;

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAuthClaimsProvider claimsProvider)
        : base(options, logger, encoder)
    {
        _claimsProvider = claimsProvider;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(_claimsProvider.Claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed class TestAuthClaimsProvider
{
    public TestAuthClaimsProvider(IReadOnlyList<Claim> claims)
    {
        Claims = claims;
    }

    public IReadOnlyList<Claim> Claims { get; }
}
