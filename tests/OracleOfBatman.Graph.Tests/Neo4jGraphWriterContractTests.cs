using Neo4j.Driver;
using OracleOfBatman.Domain;

namespace OracleOfBatman.Graph.Tests;

/// <summary>
///   Runs the shared IGraphStore contract (GraphStoreContractTests) against a real Neo4j
///   (Testcontainers, not mocked — ADR-0006), sharing one container for the whole "Neo4j"
///   collection (Neo4jContainerFixture) instead of spinning one up per test — container
///   startup, not query time, was the dominant cost. Requires Docker; run separately from the
///   rest of the suite (`dotnet test --filter Category=Integration`). Also holds Neo4j-only
///   coverage that doesn't fit the shared contract: raw Cypher wire format and GetSummaryAsync,
///   which isn't part of IGraphStore at all.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Neo4j")]
public sealed class Neo4jGraphWriterContractTests(Neo4jContainerFixture fixture) : GraphStoreContractTests
{
  protected override IGraphStore CreateStore() => new Neo4jGraphWriter(fixture.Driver);

  [Fact]
  public async Task UpsertCharacter_IsIdempotent()
  {
    // Counts nodes matching this test's own id, not the whole graph — the container (and
    // so the database) is shared across every test in this class.
    var characterId = NextId();
    var writer = new Neo4jGraphWriter(fixture.Driver);
    var character = new Character(characterId, "A");

    await writer.UpsertCharacterAsync(character);
    await writer.UpsertCharacterAsync(character);

    var count = await CountCharacterNodesAsync(characterId);
    Assert.Equal(1, count);
  }

  [Fact]
  public async Task UpsertCharacter_PersistsImageUrlAndSiteDetailUrl()
  {
    var characterId = NextId();
    var writer = new Neo4jGraphWriter(fixture.Driver);
    var character = new Character(characterId, "A", imageUrl: "https://example.com/jim-icon.jpg",
      siteDetailUrl: "https://comicvine.gamespot.com/jim-hammond/4005-12605/");

    await writer.UpsertCharacterAsync(character);

    var (imageUrl, siteDetailUrl) = await ReadCharacterUrlsAsync(characterId);
    Assert.Equal("https://example.com/jim-icon.jpg", imageUrl);
    Assert.Equal("https://comicvine.gamespot.com/jim-hammond/4005-12605/", siteDetailUrl);
  }

  [Fact]
  public async Task FindShortestPathAsync_ReturnsHops_WithFullCharacterAndIssueDataPreserved_NotJustIdAndName()
  {
    // The shortestPath() nodes carry every property already on them — hop reconstruction
    // must map all of it, not just comic_vine_id/name, or avatars/links/volume info
    // silently vanish from a found Path even though they're persisted. Neo4j-specific:
    // FakeGraphStore stores/returns the actual objects directly, so it can't have this bug.
    var characterAId = NextId();
    var characterBId = NextId();
    var issueId = NextId();
    var writer = new Neo4jGraphWriter(fixture.Driver);
    var characterA = new Character(characterAId, "A", imageUrl: "https://example.com/jim.jpg",
      siteDetailUrl: "https://comicvine.gamespot.com/jim-hammond/4005-12605/");
    var characterB = new Character(characterBId, "B", imageUrl: "https://example.com/jeff.jpg",
      siteDetailUrl: "https://comicvine.gamespot.com/jeff-the-land-shark/4005-157242/");
    await writer.UpsertCharacterAsync(characterA);
    await writer.UpsertCreditedInAsync(characterAId, [new Issue(issueId, "Some Issue")]);
    await writer.UpsertCharacterAsync(characterB);
    await writer.UpsertCreditedInAsync(characterBId, [new Issue(issueId, "Some Issue")]);
    await writer.UpsertIssueAsync(new Issue(issueId, "Some Issue", imageUrl: "https://example.com/cover.jpg",
      siteDetailUrl: "https://comicvine.gamespot.com/some-issue/4000-739613/", volumeId: 9,
      volumeName: "The Volume Title"));

    var path = await writer.FindShortestPathAsync(characterAId, characterBId, 5);

    Assert.NotNull(path);
    Assert.Equal(characterA, path.Characters[0]);
    Assert.Equal(characterB, path.Characters[1]);
    var hop = Assert.Single(path.Hops);
    Assert.Equal("https://example.com/cover.jpg", hop.Issue.ImageUrl);
    Assert.Equal("https://comicvine.gamespot.com/some-issue/4000-739613/", hop.Issue.SiteDetailUrl);
    Assert.Equal(9, hop.Issue.VolumeId);
    Assert.Equal("The Volume Title", hop.Issue.VolumeName);
  }

  [Fact]
  public async Task GetLeastRecentlyIngestedCharacterAsync_ReturnsOldestExcludingGivenIds()
  {
    // Whole-graph scan by nature (ADR-0016), so it can't share the ambient
    // shared-container data the way every id-scoped contract test above does — wipe first
    // so this class's accumulated data from other tests doesn't interfere. Safe here: tests
    // in this class run sequentially (same xUnit collection), and none of them depend on a
    // previous test's data still existing.
    await ClearGraphAsync();
    var writer = new Neo4jGraphWriter(fixture.Driver);
    var oldestId = NextId();
    var middleId = NextId();
    var newestId = NextId();
    await writer.UpsertCharacterAsync(new Character(oldestId, "A",
      ingestionDateTime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    await writer.UpsertCharacterAsync(new Character(middleId, "B",
      ingestionDateTime: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
    await writer.UpsertCharacterAsync(new Character(newestId, "C",
      ingestionDateTime: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

    var found = await writer.GetLeastRecentlyIngestedCharacterAsync([oldestId]);

    Assert.Equal(middleId, found!.ComicVineId);
  }

  private async Task ClearGraphAsync()
  {
    await using var session = fixture.Driver.AsyncSession();
    var cursor = await session.RunAsync("MATCH (n) DETACH DELETE n");
    await cursor.ConsumeAsync();
  }

  private async Task<long> CountCharacterNodesAsync(int comicVineId)
  {
    await using var session = fixture.Driver.AsyncSession();
    var cursor = await session.RunAsync(
      "MATCH (c:Character {comic_vine_id: $id}) RETURN count(c) AS count",
      new { id = comicVineId });
    var record = await cursor.SingleAsync();
    return record["count"].As<long>();
  }

  private async Task<(string? ImageUrl, string? SiteDetailUrl)> ReadCharacterUrlsAsync(int comicVineId)
  {
    await using var session = fixture.Driver.AsyncSession();
    var cursor = await session.RunAsync(
      "MATCH (c:Character {comic_vine_id: $id}) RETURN c.image_url AS imageUrl, c.site_detail_url AS siteDetailUrl",
      new { id = comicVineId });
    var record = await cursor.SingleAsync();
    return (record["imageUrl"].As<string?>(), record["siteDetailUrl"].As<string?>());
  }
}
