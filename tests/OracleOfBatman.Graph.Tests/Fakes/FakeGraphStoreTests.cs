using OracleOfBatman.Domain;

namespace OracleOfBatman.Graph.Tests.Fakes;

/// <summary>
///   Direct unit tests for FakeGraphStore's own materialization behavior (ADR-0015) — mirrors
///   IssueCreditsOverlapTests.cs's real-Neo4j coverage so ConnectionCrawlerTests can trust the
///   Fake's decision logic without needing Docker.
/// </summary>
public class FakeGraphStoreTests
{
  [Fact]
  public async Task GetIssueAsync_ReturnsNullWhenNotYetMaterialized()
  {
    var store = new FakeGraphStore();

    var found = await store.GetIssueAsync(1101757);

    Assert.Null(found);
  }

  [Fact]
  public async Task FindOverlappingIssues_MaterializesAnIssueNode_WhenTwoCharactersShareIt()
  {
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(new Character(1, "A"));
    await store.UpsertCharacterIssueCreditsAsync(1, [739613]);
    await store.UpsertCharacterAsync(new Character(2, "B"));

    await store.FindOverlappingIssuesAsync(2, [739613, 717540]);

    var materialized = await store.GetIssueAsync(739613);
    Assert.NotNull(materialized);
    Assert.Equal(739613, materialized.ComicVineId);
    Assert.Equal([1, 2], store.IssueCharacterCredits[739613].OrderBy(id => id));
  }

  [Fact]
  public async Task FindOverlappingIssues_DoesNotMaterializeAnIssueNode_WhenNoOverlapExists()
  {
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(new Character(1, "A"));
    await store.UpsertCharacterIssueCreditsAsync(1, [739613]);
    await store.UpsertCharacterAsync(new Character(2, "B"));

    await store.FindOverlappingIssuesAsync(2, [717540]);

    Assert.Null(await store.GetIssueAsync(739613));
  }

  [Fact]
  public async Task FindOverlappingIssues_AccumulatesCharacterCreditsAcrossSeparateOverlapDiscoveries()
  {
    // The D-discovery mechanism central to ADR-0015: the same Issue's character_credits
    // grows as unrelated crawl runs each separately confirm they share it, not just the
    // pair that first materialized it.
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(new Character(1, "A"));
    await store.UpsertCharacterIssueCreditsAsync(1, [739613]);
    await store.UpsertCharacterAsync(new Character(2, "B"));
    await store.FindOverlappingIssuesAsync(2, [739613]);

    await store.UpsertCharacterAsync(new Character(3, "C"));
    await store.FindOverlappingIssuesAsync(3, [739613]);

    Assert.Equal([1, 2, 3], store.IssueCharacterCredits[739613].OrderBy(id => id));
  }

  [Fact]
  public async Task PathExistsAsync_TrueViaSharedMaterializedIssue()
  {
    // ADR-0015 Slice 5: pathfinding is array-based now — no UpsertConnectionAsync call
    // anywhere in this setup. The old CONNECTION-adjacency BFS must not be what's
    // answering this.
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(new Character(12605, "Jim Hammond"));
    await store.UpsertCharacterIssueCreditsAsync(12605, [739613]);
    await store.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));
    await store.FindOverlappingIssuesAsync(157242, [739613]);

    var pathExists = await store.PathExistsAsync(12605, 157242);

    Assert.True(pathExists);
  }

  [Fact]
  public async Task PathExistsAsync_TrueViaMultiHopThroughSeparateMaterializedIssues()
  {
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(new Character(12605, "Jim Hammond"));
    await store.UpsertCharacterIssueCreditsAsync(12605, [111]);
    await store.UpsertCharacterAsync(new Character(125054, "Gwenpool"));
    await store.UpsertCharacterIssueCreditsAsync(125054, [111, 222]);
    await store.FindOverlappingIssuesAsync(125054, [111, 222]); // confirms Jim<->Gwenpool via 111
    await store.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));
    await store.FindOverlappingIssuesAsync(157242, [222]); // confirms Gwenpool<->Jeff via 222

    var pathExists = await store.PathExistsAsync(12605, 157242);

    Assert.True(pathExists);
  }

  [Fact]
  public async Task PathExistsAsync_FalseWhenNoMaterializedIssueConnectsThem()
  {
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(new Character(12605, "Jim Hammond"));
    await store.UpsertCharacterIssueCreditsAsync(12605, [111]);
    await store.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));
    await store.UpsertCharacterIssueCreditsAsync(157242, [222]);

    var pathExists = await store.PathExistsAsync(12605, 157242);

    Assert.False(pathExists);
  }

  [Fact]
  public async Task FindShortestPathAsync_ReturnsOneHopPath_ViaSharedMaterializedIssue()
  {
    var jimHammond = new Character(12605, "Jim Hammond");
    var jeff = new Character(157242, "Jeff the Land Shark");
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(jimHammond);
    await store.UpsertCharacterIssueCreditsAsync(12605, [739613]);
    await store.UpsertCharacterAsync(jeff);
    await store.FindOverlappingIssuesAsync(157242, [739613]);

    var path = await store.FindShortestPathAsync(12605, 157242, 5);

    Assert.NotNull(path);
    Assert.Equal(1, path.BatmanNumber);
    Assert.Equal([jimHammond, jeff], path.Characters);
    var hop = Assert.Single(path.Hops);
    Assert.Equal(jimHammond, hop.From);
    Assert.Equal(jeff, hop.To);
    Assert.Equal(739613, hop.Issue.ComicVineId);
  }

  [Fact]
  public async Task FindShortestPathAsync_ReturnsNull_WhenShortestPathExceedsMaxDepth()
  {
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(new Character(1, "A"));
    await store.UpsertCharacterIssueCreditsAsync(1, [111]);
    await store.UpsertCharacterAsync(new Character(2, "B"));
    await store.UpsertCharacterIssueCreditsAsync(2, [111, 222]);
    await store.FindOverlappingIssuesAsync(2, [111, 222]);
    await store.UpsertCharacterAsync(new Character(3, "C"));
    await store.FindOverlappingIssuesAsync(3, [222]);

    var path = await store.FindShortestPathAsync(1, 3, 1);

    Assert.Null(path);
  }

  [Fact]
  public async Task PathExistsAsync_TrueViaEstablishedConnection_WithoutAnyMaterializedIssueData()
  {
    // The fast path: an already-cached Connection answers this without touching the
    // array-based machinery at all.
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(new Character(12605, "Jim Hammond"));
    await store.UpsertCharacterAsync(new Character(157242, "Jeff the Land Shark"));
    await store.UpsertConnectionAsync(new Connection(12605, 157242, 739613, null));

    var pathExists = await store.PathExistsAsync(12605, 157242);

    Assert.True(pathExists);
  }

  [Fact]
  public async Task FindShortestPathAsync_ReturnsPathFromAnEstablishedConnection_WithoutAnyMaterializedIssueData()
  {
    var jimHammond = new Character(12605, "Jim Hammond");
    var jeff = new Character(157242, "Jeff the Land Shark");
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(jimHammond);
    await store.UpsertCharacterAsync(jeff);
    await store.UpsertConnectionAsync(new Connection(12605, 157242, 739613, null));

    var path = await store.FindShortestPathAsync(12605, 157242, 5);

    Assert.NotNull(path);
    var hop = Assert.Single(path.Hops);
    Assert.Equal(jimHammond, hop.From);
    Assert.Equal(jeff, hop.To);
    Assert.Equal(739613, hop.Issue.ComicVineId);
  }

  [Fact]
  public async Task FindShortestPathAsync_WritesAConnection_ForANewlyDiscoveredPathViaTheArrayFallback()
  {
    // The array-based fallback found this — nothing was established yet. It should
    // get cached as a real Connection so the next lookup for this pair hits the fast
    // path instead of re-walking issue_credits.
    var jimHammond = new Character(12605, "Jim Hammond");
    var jeff = new Character(157242, "Jeff the Land Shark");
    var store = new FakeGraphStore();
    await store.UpsertCharacterAsync(jimHammond);
    await store.UpsertCharacterIssueCreditsAsync(12605, [739613]);
    await store.UpsertCharacterAsync(jeff);
    await store.FindOverlappingIssuesAsync(157242, [739613]);

    var path = await store.FindShortestPathAsync(12605, 157242, 5);

    Assert.NotNull(path);
    Assert.Contains(store.Connections, c => c.ComicIssueId == 739613);
  }
}
