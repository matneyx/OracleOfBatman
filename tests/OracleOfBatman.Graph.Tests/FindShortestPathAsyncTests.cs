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
  public async Task ReturnsOneHopPath_ViaSharedMaterializedIssue()
  {
    // ADR-0015 Slice 5: array-based now — the overlap must be confirmed via
    // FindOverlappingIssuesAsync, not written as a CONNECTION edge.
    var jimHammond = new Character(12605, "Jim Hammond");
    var jeff = new Character(157242, "Jeff the Land Shark");
    await _writer.UpsertCharacterAsync(jimHammond);
    await _writer.UpsertCharacterIssueCreditsAsync(12605, [739613]);
    await _writer.UpsertCharacterAsync(jeff);
    await _writer.FindOverlappingIssuesAsync(157242, [739613]);

    var path = await _writer.FindShortestPathAsync(12605, 157242, 5);

    Assert.NotNull(path);
    Assert.Equal(1, path.BatmanNumber);
    Assert.Equal([jimHammond, jeff], path.Characters);
    var hop = Assert.Single(path.Hops);
    Assert.Equal(jimHammond, hop.From);
    Assert.Equal(jeff, hop.To);
    Assert.Equal(739613, hop.Issue.ComicVineId);
  }

  [Fact]
  public async Task ReturnsHopsInWalkOrder_ThroughSeparateMaterializedIssues()
  {
    var softServe = new Character(176719, "Soft Serve");
    var beast = new Character(15694, "Beast");
    var bloodscream = new Character(15734, "Bloodscream");
    await _writer.UpsertCharacterAsync(softServe);
    await _writer.UpsertCharacterIssueCreditsAsync(176719, [111]);
    await _writer.UpsertCharacterAsync(beast);
    await _writer.UpsertCharacterIssueCreditsAsync(15694, [111, 222]);
    await _writer.FindOverlappingIssuesAsync(15694, [111, 222]); // confirms Soft Serve<->Beast via 111
    await _writer.UpsertCharacterAsync(bloodscream);
    await _writer.FindOverlappingIssuesAsync(15734, [222]); // confirms Beast<->Bloodscream via 222

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
  public async Task ReturnsNull_WhenShortestPathExceedsMaxDepth()
  {
    var a = new Character(1, "A");
    var b = new Character(2, "B");
    var c = new Character(3, "C");
    await _writer.UpsertCharacterAsync(a);
    await _writer.UpsertCharacterIssueCreditsAsync(1, [111]);
    await _writer.UpsertCharacterAsync(b);
    await _writer.UpsertCharacterIssueCreditsAsync(2, [111, 222]);
    await _writer.FindOverlappingIssuesAsync(2, [111, 222]);
    await _writer.UpsertCharacterAsync(c);
    await _writer.FindOverlappingIssuesAsync(3, [222]);

    var path = await _writer.FindShortestPathAsync(1, 3, 1);

    Assert.Null(path);
  }

  [Fact]
  public async Task ReturnsPathFromAnEstablishedConnection_WithoutAnyMaterializedIssueData()
  {
    // The fast path: an already-cached Connection answers this directly — no
    // issue_credits, no materialized Issue, none of the array-based machinery at all.
    var jimHammond = new Character(12605, "Jim Hammond");
    var jeff = new Character(157242, "Jeff the Land Shark");
    await _writer.UpsertCharacterAsync(jimHammond);
    await _writer.UpsertCharacterAsync(jeff);
    await _writer.UpsertConnectionAsync(new Connection(12605, 157242, 739613, null));

    var path = await _writer.FindShortestPathAsync(12605, 157242, 5);

    Assert.NotNull(path);
    Assert.Equal(1, path.BatmanNumber);
    var hop = Assert.Single(path.Hops);
    Assert.Equal(jimHammond, hop.From);
    Assert.Equal(jeff, hop.To);
    Assert.Equal(739613, hop.Issue.ComicVineId);
  }

  [Fact]
  public async Task WritesAConnection_ForEachHopOfANewlyDiscoveredPath()
  {
    // The array-based fallback is what found this — nothing was established yet.
    // Its hops should get cached as real Connections so the next lookup for this
    // pair (or an overlapping one) hits the fast path instead.
    var jimHammond = new Character(12605, "Jim Hammond");
    var jeff = new Character(157242, "Jeff the Land Shark");
    await _writer.UpsertCharacterAsync(jimHammond);
    await _writer.UpsertCharacterIssueCreditsAsync(12605, [739613]);
    await _writer.UpsertCharacterAsync(jeff);
    await _writer.FindOverlappingIssuesAsync(157242, [739613]);

    var path = await _writer.FindShortestPathAsync(12605, 157242, 5);

    Assert.NotNull(path);
    var (_, connectionCount) = await _writer.GetSummaryAsync();
    Assert.Equal(1, connectionCount);
  }

  [Fact]
  public async Task WritesAConnection_ForEveryHopOfAMultiHopDiscoveredPath()
  {
    var softServe = new Character(176719, "Soft Serve");
    var beast = new Character(15694, "Beast");
    var bloodscream = new Character(15734, "Bloodscream");
    await _writer.UpsertCharacterAsync(softServe);
    await _writer.UpsertCharacterIssueCreditsAsync(176719, [111]);
    await _writer.UpsertCharacterAsync(beast);
    await _writer.UpsertCharacterIssueCreditsAsync(15694, [111, 222]);
    await _writer.FindOverlappingIssuesAsync(15694, [111, 222]);
    await _writer.UpsertCharacterAsync(bloodscream);
    await _writer.FindOverlappingIssuesAsync(15734, [222]);

    var path = await _writer.FindShortestPathAsync(176719, 15734, 5);

    Assert.NotNull(path);
    var (_, connectionCount) = await _writer.GetSummaryAsync();
    Assert.Equal(2, connectionCount);
  }
}
