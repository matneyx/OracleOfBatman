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

    [Fact]
    public async Task UpsertCharacter_PersistsImageUrlAndSiteDetailUrl()
    {
        var writer = new Neo4jGraphWriter(_driver);
        var jimHammond = new Character(12605, "Jim Hammond", "https://example.com/jim-icon.jpg", "https://comicvine.gamespot.com/jim-hammond/4005-12605/");

        await writer.UpsertCharacterAsync(jimHammond);

        var (imageUrl, siteDetailUrl) = await ReadCharacterUrlsAsync(12605);
        Assert.Equal("https://example.com/jim-icon.jpg", imageUrl);
        Assert.Equal("https://comicvine.gamespot.com/jim-hammond/4005-12605/", siteDetailUrl);
    }

    [Fact]
    public async Task UpsertConnection_PersistsComicIssueNameAndSiteDetailUrl()
    {
        var writer = new Neo4jGraphWriter(_driver);
        await writer.UpsertCharacterAsync(new Character(125054, "Gwenpool"));
        await writer.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));

        var connection = new Connection(
            125054, 157242, 1101757, null, InteractionTier.SameIssue, Confidence.Unverified,
            "Spoonful of Everything – Part 2!",
            "https://comicvine.gamespot.com/its-jeff-infinity-comic-45-spoonful-of-everything-/4000-1101757/");
        await writer.UpsertConnectionAsync(connection);

        var (issueName, issueSiteDetailUrl) = await ReadConnectionIssueDetailsAsync(1101757);
        Assert.Equal("Spoonful of Everything – Part 2!", issueName);
        Assert.Equal("https://comicvine.gamespot.com/its-jeff-infinity-comic-45-spoonful-of-everything-/4000-1101757/", issueSiteDetailUrl);
    }

    [Fact]
    public async Task UpsertConnection_SymmetricTier_IsIdempotentRegardlessOfSourceTargetOrder()
    {
        var writer = new Neo4jGraphWriter(_driver);
        await writer.UpsertCharacterAsync(new Character(125054, "Gwenpool"));
        await writer.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));

        // Same real-world Same Issue connection, but written with Source/Target swapped —
        // e.g. because a later crawl discovered the pair in the opposite order. Same Issue is
        // Symmetric (CONTEXT.md), so this must still resolve to exactly one relationship.
        await writer.UpsertConnectionAsync(new Connection(125054, 157242, 1101757, null, InteractionTier.SameIssue, Confidence.Unverified));
        await writer.UpsertConnectionAsync(new Connection(157242, 125054, 1101757, null, InteractionTier.SameIssue, Confidence.Unverified));

        var (_, connectionCount) = await writer.GetSummaryAsync();
        Assert.Equal(1, connectionCount);
    }

    [Fact]
    public async Task UpsertConnection_DirectionalTier_KeepsOppositeDirectionAsADistinctRelationship()
    {
        var writer = new Neo4jGraphWriter(_driver);
        await writer.UpsertCharacterAsync(new Character(125054, "Gwenpool"));
        await writer.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));

        // In-Universe Mention is Directional (CONTEXT.md) — Gwenpool mentioning Jeff is a
        // different fact than Jeff mentioning Gwenpool, so both must be kept.
        await writer.UpsertConnectionAsync(new Connection(125054, 157242, 1101757, null, InteractionTier.InUniverseMention, Confidence.Unverified));
        await writer.UpsertConnectionAsync(new Connection(157242, 125054, 1101757, null, InteractionTier.InUniverseMention, Confidence.Unverified));

        var (_, connectionCount) = await writer.GetSummaryAsync();
        Assert.Equal(2, connectionCount);
    }

    private async Task<(string? ImageUrl, string? SiteDetailUrl)> ReadCharacterUrlsAsync(int comicVineId)
    {
        await using var session = _driver.AsyncSession();
        var cursor = await session.RunAsync(
            "MATCH (c:Character {comic_vine_id: $id}) RETURN c.image_url AS imageUrl, c.site_detail_url AS siteDetailUrl",
            new { id = comicVineId });
        var record = await cursor.SingleAsync();
        return (record["imageUrl"].As<string?>(), record["siteDetailUrl"].As<string?>());
    }

    private async Task<(string? IssueName, string? IssueSiteDetailUrl)> ReadConnectionIssueDetailsAsync(int comicIssueId)
    {
        await using var session = _driver.AsyncSession();
        var cursor = await session.RunAsync(
            "MATCH ()-[r:CONNECTION {comic_issue_id: $issueId}]-() RETURN r.comic_issue_name AS issueName, r.comic_issue_site_detail_url AS issueSiteDetailUrl",
            new { issueId = comicIssueId });
        var record = await cursor.SingleAsync();
        return (record["issueName"].As<string?>(), record["issueSiteDetailUrl"].As<string?>());
    }
}
