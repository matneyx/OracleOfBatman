using Neo4j.Driver;
using OracleOfBatman.Domain;
using OracleOfBatman.Graph;
using Testcontainers.Neo4j;
using Xunit;

namespace OracleOfBatman.Graph.Tests;

/// <summary>
/// Against a real, ephemeral Neo4j (Testcontainers), not mocked — matches ADR-0006's e2e
/// approach. Requires Docker; run separately from the rest of the suite
/// (`dotnet test --filter Category=Integration`).
/// </summary>
[Trait("Category", "Integration")]
public sealed class Neo4jGraphWriterTests : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5").Build();
    private IDriver _driver = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // Testcontainers' Neo4jBuilder defaults NEO4J_AUTH to "none" — no credentials needed.
        _driver = GraphDatabase.Driver(_container.GetConnectionString(), AuthTokens.None);
    }

    public async Task DisposeAsync()
    {
        await _driver.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task UpsertCharacter_IsIdempotent()
    {
        var writer = new Neo4jGraphWriter(_driver);
        var jeff = new Character(157242, "Jeff the Land Shark");

        await writer.UpsertCharacterAsync(jeff);
        await writer.UpsertCharacterAsync(jeff);

        var (characterCount, _) = await writer.GetSummaryAsync();
        Assert.Equal(1, characterCount);
    }

    [Fact]
    public async Task UpsertConnection_CreatesOneRelationshipBetweenExistingCharacters()
    {
        var writer = new Neo4jGraphWriter(_driver);
        await writer.UpsertCharacterAsync(new Character(125054, "Gwenpool"));
        await writer.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));

        var connection = new Connection(125054, 157242, 1101757, new DateOnly(2025, 4, 4), InteractionTier.SameIssue, Confidence.Unverified);
        await writer.UpsertConnectionAsync(connection);
        await writer.UpsertConnectionAsync(connection);

        var (characterCount, connectionCount) = await writer.GetSummaryAsync();
        Assert.Equal(2, characterCount);
        Assert.Equal(1, connectionCount);
    }

    [Fact]
    public async Task PathExists_FalseWhenCharactersAreUnconnected()
    {
        var writer = new Neo4jGraphWriter(_driver);
        await writer.UpsertCharacterAsync(new Character(12605, "Jim Hammond"));
        await writer.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));

        var pathExists = await writer.PathExistsAsync(12605, 157242);

        Assert.False(pathExists);
    }

    [Fact]
    public async Task PathExists_TrueViaDirectConnection()
    {
        var writer = new Neo4jGraphWriter(_driver);
        await writer.UpsertCharacterAsync(new Character(12605, "Jim Hammond"));
        await writer.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));
        await writer.UpsertConnectionAsync(new Connection(12605, 157242, 1101757, null, InteractionTier.SameIssue, Confidence.Unverified));

        var pathExists = await writer.PathExistsAsync(12605, 157242);

        Assert.True(pathExists);
    }

    [Fact]
    public async Task PathExists_TrueViaMultiHopConnectionRegardlessOfRelationshipDirection()
    {
        var writer = new Neo4jGraphWriter(_driver);
        await writer.UpsertCharacterAsync(new Character(12605, "Jim Hammond"));
        await writer.UpsertCharacterAsync(new Character(125054, "Gwenpool"));
        await writer.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));
        await writer.UpsertConnectionAsync(new Connection(12605, 125054, 111, null, InteractionTier.SameIssue, Confidence.Unverified));
        await writer.UpsertConnectionAsync(new Connection(157242, 125054, 222, null, InteractionTier.SameIssue, Confidence.Unverified));

        var pathExists = await writer.PathExistsAsync(12605, 157242);

        Assert.True(pathExists);
    }

    [Fact]
    public async Task PathExists_FalseWhenEitherCharacterIsNotYetInTheGraph()
    {
        var writer = new Neo4jGraphWriter(_driver);
        await writer.UpsertCharacterAsync(new Character(12605, "Jim Hammond"));

        var pathExists = await writer.PathExistsAsync(12605, 157242);

        Assert.False(pathExists);
    }
}
