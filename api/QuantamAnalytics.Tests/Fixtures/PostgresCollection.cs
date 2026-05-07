namespace QuantamAnalytics.Tests.Fixtures;

/// <summary>
/// xUnit collection that ties test classes to a single shared
/// <see cref="PostgresFixture"/>. Putting <c>[Collection(nameof(PostgresCollection))]</c>
/// on a test class injects the same container instance across every test
/// in the collection, so we pay the container-startup cost once per run.
/// </summary>
[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    // Marker class only — collection wiring lives in the attribute above.
}
