using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using QuantamAnalytics.Infrastructure;

namespace QuantamAnalytics.Tests.Fixtures;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> wired against the
/// ephemeral Postgres container instead of the developer's user-secrets
/// Neon connection. Plug in the container's connection string and the
/// rest of <c>Program.cs</c> composes normally.
/// </summary>
/// <remarks>
/// The connection string is set via <see cref="IWebHostBuilder.UseSetting"/>
/// before the host builds, so <see cref="DependencyInjection.AddInfrastructure"/>
/// reads it from <c>IConfiguration</c> the way it would in any other
/// environment. No DbContext re-registration, no service replacement —
/// the goal is to exercise the production wiring path.
///
/// Calling <c>WithAuthenticatedUser(...)</c> (from
/// <see cref="QuantamAnalytics.Tests.TestAuth.TestAuthFactoryExtensions"/>)
/// returns a derived factory that inherits this connection-string override,
/// so per-tenant clients all hit the same database.
/// </remarks>
public sealed class IsolatedAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public IsolatedAppFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            $"ConnectionStrings:{DependencyInjection.PostgresConnectionStringName}",
            _connectionString);
    }
}
