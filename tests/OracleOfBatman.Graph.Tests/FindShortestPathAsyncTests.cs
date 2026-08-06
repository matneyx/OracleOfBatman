using Neo4j.Driver;
using OracleOfBatman.Domain;
using Testcontainers.Neo4j;

namespace OracleOfBatman.Graph.Tests;

[Trait("Category", "Integration")]
public sealed class FindShortestPathAsyncTests : IAsyncLifetime
{
  private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5").Build();
  private IDriver _driver = null!;
  private Neo4jGraphWriter _writer = null!;

  public async Task InitializeAsync()
  {
    await _container.StartAsync();
    _driver = GraphDatabase.Driver(_container.GetConnectionString(), AuthTokens.None);
    _writer = new Neo4jGraphWriter(_driver);
  }

  public async Task DisposeAsync()
  {
    await _driver.DisposeAsync();
    await _container.DisposeAsync();
  }

  [Fact]
  public async Task ReturnsNull_WhenEitherCharacterDoesNotExist()
  {
    await _writer.UpsertCharacterAsync(new Character(12605, "Jim Hammond"));

    var path = await _writer.FindShortestPathAsync(12605, 999999, 5);

    Assert.Null(path);
  }

  [Fact]
  public async Task ReturnsNull_WhenNoPathExists()
  {
    await _writer.UpsertCharacterAsync(new Character(12605, "Jim Hammond"));
    await _writer.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));

    var path = await _writer.FindShortestPathAsync(12605, 157242, 5);

    Assert.Null(path);
  }

  [Fact]
  public async Task ReturnsOneHopPath_ForDirectConnection()
  {
    var jimHammond = new Character(12605, "Jim Hammond");
    var jeff = new Character(157242, "Jeff the Land Shark");
    await _writer.UpsertCharacterAsync(jimHammond);
    await _writer.UpsertCharacterAsync(jeff);
    await _writer.UpsertConnectionAsync(new Connection(12605, 157242, 739613, null, InteractionTier.SameIssue,
      Confidence.Unverified));

    var path = await _writer.FindShortestPathAsync(12605, 157242, 5);

    Assert.NotNull(path);
    Assert.Equal(1, path.BatmanNumber);
    Assert.Equal([jimHammond, jeff], path.Characters);
    var hop = Assert.Single(path.Hops);
    Assert.Equal(jimHammond, hop.From);
    Assert.Equal(jeff, hop.To);
    Assert.Equal(739613, hop.ComicIssueId);
    Assert.Equal(InteractionTier.SameIssue, hop.Tier);
    Assert.Equal(Confidence.Unverified, hop.Confidence);
  }

  [Fact]
  public async Task ReturnsHopsInWalkOrder_RegardlessOfStoredRelationshipDirection()
  {
    var softServe = new Character(176719, "Soft Serve");
    var beast = new Character(15694, "Beast");
    var bloodscream = new Character(15734, "Bloodscream");
    await _writer.UpsertCharacterAsync(softServe);
    await _writer.UpsertCharacterAsync(beast);
    await _writer.UpsertCharacterAsync(bloodscream);
    // Stored "backwards" relative to the walk direction we'll query in.
    await _writer.UpsertConnectionAsync(new Connection(15694, 176719, 111, null, InteractionTier.SameIssue,
      Confidence.Unverified));
    await _writer.UpsertConnectionAsync(new Connection(15734, 15694, 222, null, InteractionTier.SameIssue,
      Confidence.Unverified));

    var path = await _writer.FindShortestPathAsync(176719, 15734, 5);

    Assert.NotNull(path);
    Assert.Equal([softServe, beast, bloodscream], path.Characters);
    Assert.Equal(2, path.Hops.Count);
    Assert.Equal(softServe, path.Hops[0].From);
    Assert.Equal(beast, path.Hops[0].To);
    Assert.Equal(beast, path.Hops[1].From);
    Assert.Equal(bloodscream, path.Hops[1].To);
  }

  [Fact]
  public async Task ReturnsCharacterImageSiteDetailUrl_AndHopIssueNameAndSiteDetailUrl()
  {
    var jimHammond = new Character(12605, "Jim Hammond", "https://example.com/jim-icon.jpg",
      "https://comicvine.gamespot.com/jim-hammond/4005-12605/");
    var jeff = new Character(157242, "Jeff the Land Shark", "https://example.com/jeff-icon.jpg",
      "https://comicvine.gamespot.com/jeff-the-land-shark/4005-157242/");
    await _writer.UpsertCharacterAsync(jimHammond);
    await _writer.UpsertCharacterAsync(jeff);
    await _writer.UpsertConnectionAsync(new Connection(
      12605, 157242, 739613, null, InteractionTier.SameIssue, Confidence.Unverified,
      "Some Issue", "https://comicvine.gamespot.com/some-issue/4000-739613/"));

    var path = await _writer.FindShortestPathAsync(12605, 157242, 5);

    Assert.NotNull(path);
    Assert.Equal(jimHammond, path.Characters[0]);
    Assert.Equal(jeff, path.Characters[1]);
    var hop = Assert.Single(path.Hops);
    Assert.Equal("Some Issue", hop.ComicIssueName);
    Assert.Equal("https://comicvine.gamespot.com/some-issue/4000-739613/", hop.ComicIssueSiteDetailUrl);
  }

  [Fact]
  public async Task ReturnsNull_WhenShortestPathExceedsMaxDepth()
  {
    var a = new Character(1, "A");
    var b = new Character(2, "B");
    var c = new Character(3, "C");
    await _writer.UpsertCharacterAsync(a);
    await _writer.UpsertCharacterAsync(b);
    await _writer.UpsertCharacterAsync(c);
    await _writer.UpsertConnectionAsync(new Connection(1, 2, 111, null, InteractionTier.SameIssue,
      Confidence.Unverified));
    await _writer.UpsertConnectionAsync(new Connection(2, 3, 222, null, InteractionTier.SameIssue,
      Confidence.Unverified));

    var path = await _writer.FindShortestPathAsync(1, 3, 1);

    Assert.Null(path);
  }
}
