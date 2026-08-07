using Neo4j.Driver;
using OracleOfBatman.Domain;
using Testcontainers.Neo4j;

namespace OracleOfBatman.Graph.Tests;

/// <summary>
///   ADR-0012: overlap detection must span the whole persisted graph, not just
///   characters discovered in one crawl run.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IssueCreditsOverlapTests : IAsyncLifetime
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
  public async Task FindOverlappingIssues_ReturnsOtherCharacterSharingAnIssue()
  {
    await _writer.UpsertCharacterAsync(new Character(1, "A"));
    await _writer.UpsertCharacterIssueCreditsAsync(1, [100, 200]);

    var overlaps = await _writer.FindOverlappingIssuesAsync(2, [200, 300]);

    var overlap = Assert.Single(overlaps);
    Assert.Equal(1, overlap.Key);
    Assert.Equal([200], overlap.Value);
  }

  [Fact]
  public async Task FindOverlappingIssues_ExcludesTheQueriedCharacterItself()
  {
    await _writer.UpsertCharacterAsync(new Character(1, "A"));
    await _writer.UpsertCharacterIssueCreditsAsync(1, [100, 200]);

    var overlaps = await _writer.FindOverlappingIssuesAsync(1, [100, 200]);

    Assert.Empty(overlaps);
  }

  [Fact]
  public async Task FindOverlappingIssues_ReturnsEmpty_WhenNoOverlap()
  {
    await _writer.UpsertCharacterAsync(new Character(1, "A"));
    await _writer.UpsertCharacterIssueCreditsAsync(1, [100, 200]);

    var overlaps = await _writer.FindOverlappingIssuesAsync(2, [300, 400]);

    Assert.Empty(overlaps);
  }

  [Fact]
  public async Task FindOverlappingIssues_FindsOverlapWithACharacterFromAnEarlierUnrelatedRun()
  {
    // Simulates ADR-0012's fixed gap: character 1 was persisted by some earlier,
    // unrelated crawl. A brand new crawl (character 2) should still find the overlap.
    await _writer.UpsertCharacterAsync(new Character(1, "EarlierRunCharacter"));
    await _writer.UpsertCharacterIssueCreditsAsync(1, [739613]);

    var overlaps = await _writer.FindOverlappingIssuesAsync(2, [739613, 717540]);

    var overlap = Assert.Single(overlaps);
    Assert.Equal(1, overlap.Key);
    Assert.Equal([739613], overlap.Value);
  }

  [Fact]
  public async Task FindOverlappingIssues_MaterializesAnIssueNode_WhenTwoCharactersShareIt()
  {
    // The overlap itself is what triggers materialization (ADR-0015) — an Issue node
    // never gets created just because one Character's own issue_credits mentions it.
    await _writer.UpsertCharacterAsync(new Character(1, "A"));
    await _writer.UpsertCharacterIssueCreditsAsync(1, [739613]);
    await _writer.UpsertCharacterAsync(new Character(2, "B"));

    await _writer.FindOverlappingIssuesAsync(2, [739613, 717540]);

    var materialized = await _writer.GetIssueAsync(739613);
    Assert.NotNull(materialized);
    Assert.Equal(739613, materialized.ComicVineId);

    var characterCredits = await ReadIssueCharacterCreditsAsync(739613);
    Assert.Equal([1, 2], characterCredits.OrderBy(id => id));
  }

  [Fact]
  public async Task FindOverlappingIssues_DoesNotMaterializeAnIssueNode_WhenNoOverlapExists()
  {
    await _writer.UpsertCharacterAsync(new Character(1, "A"));
    await _writer.UpsertCharacterIssueCreditsAsync(1, [739613]);
    await _writer.UpsertCharacterAsync(new Character(2, "B"));

    await _writer.FindOverlappingIssuesAsync(2, [717540]);

    var materialized = await _writer.GetIssueAsync(739613);
    Assert.Null(materialized);
  }

  [Fact]
  public async Task FindOverlappingIssues_AccumulatesCharacterCreditsAcrossSeparateOverlapDiscoveries()
  {
    // The D-discovery mechanism central to ADR-0015: the same Issue's character_credits
    // grows as unrelated crawl runs each separately confirm they share it, not just the
    // pair that first materialized it.
    await _writer.UpsertCharacterAsync(new Character(1, "A"));
    await _writer.UpsertCharacterIssueCreditsAsync(1, [739613]);
    await _writer.UpsertCharacterAsync(new Character(2, "B"));
    await _writer.FindOverlappingIssuesAsync(2, [739613]);

    await _writer.UpsertCharacterAsync(new Character(3, "C"));
    await _writer.FindOverlappingIssuesAsync(3, [739613]);

    var characterCredits = await ReadIssueCharacterCreditsAsync(739613);
    Assert.Equal([1, 2, 3], characterCredits.OrderBy(id => id));
  }

  private async Task<int[]> ReadIssueCharacterCreditsAsync(int comicVineId)
  {
    await using var session = _driver.AsyncSession();
    var cursor = await session.RunAsync(
      "MATCH (i:Issue {comic_vine_id: $id}) RETURN i.character_credits AS characterCredits",
      new { id = comicVineId });
    var record = await cursor.SingleAsync();
    return record["characterCredits"].As<List<object>>().Select(v => v.As<int>()).ToArray();
  }
}
