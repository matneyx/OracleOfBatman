using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace OracleOfBatman.Graph.Tests;

/// <summary>
///   One Testcontainers Neo4j instance shared across every test in the "Neo4j" collection
///   (ADR-0006), instead of spinning up a fresh container per test method — container
///   startup, not query time, was the dominant cost of the integration suite. Tests share the
///   underlying database, so every test must use ids from a fresh source (NextId() on
///   GraphStoreContractTests) rather than hardcoded literals, or they'd collide with data left
///   behind by other tests sharing this same container.
/// </summary>
public sealed class Neo4jContainerFixture : IAsyncLifetime
{
  private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5").Build();

  public IDriver Driver { get; private set; } = null!;

  public async Task InitializeAsync()
  {
    await _container.StartAsync();
    // Testcontainers' Neo4jBuilder defaults NEO4J_AUTH to "none" — no credentials needed.
    Driver = GraphDatabase.Driver(_container.GetConnectionString(), AuthTokens.None);
  }

  public async Task DisposeAsync()
  {
    await Driver.DisposeAsync();
    await _container.DisposeAsync();
  }
}

[CollectionDefinition("Neo4j")]
public sealed class Neo4jCollection : ICollectionFixture<Neo4jContainerFixture>;
