using Testcontainers.PostgreSql;

namespace QuantamAnalytics.Tests.Fixtures;

/// <summary>
/// Spins up a single ephemeral Postgres container, shared across every test
/// in the <see cref="PostgresCollection"/> collection. The container starts
/// once per test run and tears down at the end. Tests that need a clean
/// schema reset it themselves via <c>Database.EnsureDeleted</c> +
/// <c>Database.Migrate</c> (see <see cref="IsolatedAppFactory"/>).
/// </summary>
/// <remarks>
/// Why a container and not the developer's Neon dev DB:
/// 1. Repeatability — every run starts from a known state, no leftover rows.
/// 2. Safety — destructive setup never touches a real Neon project.
/// 3. CI-friendly — the GitHub-hosted ubuntu-24.04 runner has Docker
///    preinstalled, so this works in CI without extra services.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        // Pin a specific minor so a container image bump doesn't break tests
        // silently. Match the production-target Postgres major (Neon = 17).
        .WithImage("postgres:17-alpine")
        .WithDatabase("qa_it")
        .WithUsername("qa_it_user")
        .WithPassword("qa_it_pw")
        .Build();

    /// <summary>
    /// Connection string for the running container. Available after
    /// <see cref="InitializeAsync"/> completes.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
