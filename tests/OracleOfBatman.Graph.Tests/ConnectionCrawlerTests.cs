using OracleOfBatman.Domain;
using OracleOfBatman.Graph.ComicVine;
using OracleOfBatman.Graph.Tests.Fakes;

namespace OracleOfBatman.Graph.Tests;

/// <summary>
///   Tests the ADR-0010 crawl algorithm's decision logic against in-memory fakes —
///   no Docker/HTTP needed, so these run as fast unit tests.
/// </summary>
public class ConnectionCrawlerTests
{
  private const int SeedA = 1;
  private const int SeedB = 2;

  private static ComicVineCharacter Character(int id, string name, IEnumerable<int>? friends = null,
    IEnumerable<int>? enemies = null, IEnumerable<int>? issues = null) => new()
  {
    Id = id,
    Name = name,
    CharacterFriends = (friends ?? []).Select(f => new ComicVineCharacterRef { Id = f, Name = $"Character{f}" })
      .ToList(),
    CharacterEnemies = (enemies ?? []).Select(e => new ComicVineCharacterRef { Id = e, Name = $"Character{e}" })
      .ToList(),
    IssueCredits = (issues ?? []).Select(i => new ComicVineIssueRef { Id = i, Name = $"Issue{i}" }).ToList()
  };

  [Fact]
  public async Task AlreadyConnected_ReturnsImmediatelyWithoutFetchingAnything()
  {
    // ADR-0015 Slice 5: PathExistsAsync is array-based now — the seeds must already
    // share a materialized issue, not a CONNECTION edge.
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(SeedA, "A"));
    await graphStore.UpsertCharacterIssueCreditsAsync(SeedA, [999]);
    await graphStore.UpsertCharacterAsync(new Character(SeedB, "B"));
    await graphStore.FindOverlappingIssuesAsync(SeedB, [999]);
    var characterSource = new FakeComicVineCharacterSource([]);
    var crawler = new ConnectionCrawler(characterSource, graphStore);

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.True(result.Connected);
    Assert.Equal(0, result.CharactersFetched);
    Assert.Empty(characterSource.FetchedIds);
  }

  [Fact]
  public async Task DirectIssueOverlapBetweenSeeds_MaterializesIssueAndStopsWithoutExpanding()
  {
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", [10], issues: [500]),
      [SeedB] = Character(SeedB, "B", [20], issues: [500])
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.True(result.Connected);
    Assert.NotNull(await graphStore.GetIssueAsync(500));
    // ADR-0015 Slice 4: Same Issue no longer writes CONNECTION edges at all — the
    // materialized Issue node above is now the sole record of this connectivity.
    Assert.Empty(graphStore.Connections);
    Assert.Equal([SeedA, SeedB], characterSource.FetchedIds);
  }

  [Fact]
  public async Task DirectFriendOverlap_FetchesSharedCharacterAndConnectsBothSeedsThroughIt()
  {
    const int sharedFriend = 30;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", [sharedFriend], issues: [100]),
      [SeedB] = Character(SeedB, "B", [sharedFriend], issues: [200]),
      [sharedFriend] = Character(sharedFriend, "Shared", issues: [100, 200])
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.True(result.Connected);
    Assert.Equal(1, result.CharactersFetched);
    Assert.NotNull(await graphStore.GetIssueAsync(100));
    Assert.NotNull(await graphStore.GetIssueAsync(200));
    Assert.Empty(graphStore.Connections);
    Assert.Equal([SeedA, SeedB, sharedFriend], characterSource.FetchedIds);
  }

  [Fact]
  public async Task NoOverlapAtAll_BidirectionalBfsChecksNewCharactersAgainstFullAccumulatedSet()
  {
    const int fromA = 10;
    const int fromB = 20;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", [fromA], issues: [111]),
      // fromA bridges A (issue 111) to fromB (issue 999) — a real 3-hop chain, not a
      // dead end: A-fromA share 111, fromA-fromB share 999, fromB-B share 222.
      [fromA] = Character(fromA, "FromA", issues: [111, 999]),
      [fromB] = Character(fromB, "FromB", issues: [999, 222]),
      [SeedB] = Character(SeedB, "B", [fromB], issues: [222])
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.True(result.Connected);
    // Only discoverable if fromB's issues are checked against fromA (not just the
    // seeds) — that's the "full accumulated set" behavior this test exists to prove.
    Assert.NotNull(await graphStore.GetIssueAsync(999));
  }

  [Fact]
  public async Task BudgetExhausted_StopsAndReportsNotConnected()
  {
    const int fromA = 10;
    const int fromB = 20;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", [fromA], issues: [111]),
      // Same 3-hop chain as the full-accumulated-set test: reaching it needs both
      // fromA and fromB fetched; budget only allows the first.
      [fromA] = Character(fromA, "FromA", issues: [111, 999]),
      [fromB] = Character(fromB, "FromB", issues: [999, 222]),
      [SeedB] = Character(SeedB, "B", [fromB], issues: [222])
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 1);

    Assert.False(result.Connected);
    Assert.Equal(1, result.CharactersFetched);
  }

  [Fact]
  public async Task SmallerFrontierIsExpandedFirst()
  {
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", [10], issues: [111]),
      [SeedB] = Character(SeedB, "B", [20, 21, 22], issues: [222]),
      [10] = Character(10, "FromA", issues: [333]),
      [20] = Character(20, "FromB1", issues: [444]),
      [21] = Character(21, "FromB2", issues: [555]),
      [22] = Character(22, "FromB3", issues: [666])
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);

    // A's frontier (1 friend) is smaller than B's (3 friends) — the one expansion the
    // budget allows should come from A's side.
    await crawler.PopulateConnectionsAsync(SeedA, SeedB, 1);

    Assert.Equal([SeedA, SeedB, 10], characterSource.FetchedIds);
  }

  [Fact]
  public async Task ConnectsToACharacterFromAnEarlierUnrelatedCrawl_NotJustThisRunsDiscoveries()
  {
    const int fromA = 10;
    const int earlierRunCharacter = 999;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", [fromA], issues: [111]),
      [SeedB] = Character(SeedB, "B", issues: [222]),
      // Shares an issue with earlierRunCharacter, who this crawl never fetches — only
      // reachable via the graph-wide overlap check (ADR-0012), not an in-run dictionary.
      [fromA] = Character(fromA, "FromA", issues: [555])
    };
    var graphStore = new FakeGraphStore();
    // Simulates a character already persisted by some earlier, unrelated crawl.
    await graphStore.UpsertCharacterAsync(new Character(earlierRunCharacter, "EarlierRunCharacter"));
    await graphStore.UpsertCharacterIssueCreditsAsync(earlierRunCharacter, [555]);
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);

    await crawler.PopulateConnectionsAsync(SeedA, SeedB, 1);

    Assert.DoesNotContain(earlierRunCharacter, characterSource.FetchedIds);
    Assert.NotNull(await graphStore.GetIssueAsync(555));
    Assert.Equal([fromA, earlierRunCharacter], graphStore.IssueCharacterCredits[555].OrderBy(id => id));
  }

  [Fact]
  public async Task NeverFetchesTheSameCharacterTwiceEvenIfReachableFromBothSides()
  {
    const int mutualFriend = 30;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", [mutualFriend], issues: [111]),
      [SeedB] = Character(SeedB, "B", [mutualFriend], issues: [222]),
      [mutualFriend] = Character(mutualFriend, "Mutual", issues: [333])
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);

    await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.Single(characterSource.FetchedIds, id => id == mutualFriend);
  }

  [Fact]
  public async Task IngestCharacterAsync_PersistsAndConnectsAStandaloneCharacter()
  {
    const int newCharacterId = 42;
    const int alreadyKnownId = 999;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [newCharacterId] = Character(newCharacterId, "New", issues: [700])
    };
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(alreadyKnownId, "AlreadyKnown"));
    await graphStore.UpsertCharacterIssueCreditsAsync(alreadyKnownId, [700]);
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);

    var ingested = await crawler.IngestCharacterAsync(newCharacterId);

    Assert.Equal("New", ingested.Name);
    Assert.Contains(graphStore.Characters, c => c.ComicVineId == newCharacterId);
    Assert.NotNull(await graphStore.GetIssueAsync(700));
    Assert.Equal([newCharacterId, alreadyKnownId], graphStore.IssueCharacterCredits[700].OrderBy(id => id));
  }

  [Fact]
  public async Task IngestCharacterAsync_RaisesCharacterAddedEvent_WhenTheCharacterIsNewToTheGraph()
  {
    var characters = new Dictionary<int, ComicVineCharacter> { [42] = Character(42, "New") };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);
    Character? addedCharacter = null;
    crawler.CharacterAdded += c => addedCharacter = (Character?)c;

    await crawler.IngestCharacterAsync(42);

    Assert.NotNull(addedCharacter);
    Assert.Equal(42, addedCharacter.ComicVineId);
    Assert.Equal("New", addedCharacter.Name);
  }

  [Fact]
  public async Task IngestCharacterAsync_DoesNotRaiseCharacterAddedEvent_WhenTheCharacterAlreadyExistedInTheGraph()
  {
    const int existingId = 42;
    var characters = new Dictionary<int, ComicVineCharacter> { [existingId] = Character(existingId, "AlreadyThere") };
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(existingId, "AlreadyThere"));
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);
    var raised = false;
    crawler.CharacterAdded += _ => raised = true;

    await crawler.IngestCharacterAsync(existingId);

    Assert.False(raised);
  }

  [Fact]
  public async Task IngestCharacterAsync_RaisesIssueConnectionConfirmedEvent_ForEachSharedIssueFound()
  {
    const int newCharacterId = 42;
    const int alreadyKnownId = 999;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [newCharacterId] = new()
      {
        Id = newCharacterId,
        Name = "New",
        IssueCredits = [new ComicVineIssueRef { Id = 700, Name = "Some Issue" }]
      }
    };
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(alreadyKnownId, "AlreadyKnown"));
    await graphStore.UpsertCharacterIssueCreditsAsync(alreadyKnownId, [700]);
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);
    var confirmedIssues = new List<Issue>();
    crawler.IssueConnectionConfirmed += confirmedIssues.Add;

    await crawler.IngestCharacterAsync(newCharacterId);

    var confirmed = Assert.Single(confirmedIssues);
    Assert.Equal(700, confirmed.ComicVineId);
    Assert.Equal("Some Issue", confirmed.Name);
  }

  [Fact]
  public async Task IngestCharacterAsync_DoesNotRaiseIssueConnectionConfirmedEvent_WhenNoOverlapExists()
  {
    var characters = new Dictionary<int, ComicVineCharacter> { [42] = Character(42, "Solo", issues: [111]) };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);
    var raised = false;
    crawler.IssueConnectionConfirmed += _ => raised = true;

    await crawler.IngestCharacterAsync(42);

    Assert.False(raised);
  }

  [Fact]
  public async Task PersistCharacterAsync_PersistsTheCharacterAndIssueCredits_WithoutCheckingForOverlaps()
  {
    // New UX: picking a character from Comic Vine search shouldn't materialize Issues
    // yet — that only happens once the user actually tries to make a connection
    // (Go / "Try to find a connection"), not as a side effect of just adding a
    // character to the graph.
    const int newCharacterId = 42;
    const int alreadyKnownId = 999;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [newCharacterId] = Character(newCharacterId, "New", issues: [700])
    };
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(alreadyKnownId, "AlreadyKnown"));
    await graphStore.UpsertCharacterIssueCreditsAsync(alreadyKnownId, [700]);
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);

    var persisted = await crawler.PersistCharacterAsync(newCharacterId);

    Assert.Equal("New", persisted.Name);
    Assert.Contains(graphStore.Characters, c => c.ComicVineId == newCharacterId);
    // The real point of this test: 700 is shared with an already-known character, but
    // no overlap check has run — nothing should be materialized yet.
    Assert.Null(await graphStore.GetIssueAsync(700));
  }

  [Fact]
  public async Task PersistCharacterAsync_RaisesCharacterAddedEvent_WhenTheCharacterIsNewToTheGraph()
  {
    var characters = new Dictionary<int, ComicVineCharacter> { [42] = Character(42, "New") };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);
    Character? addedCharacter = null;
    crawler.CharacterAdded += c => addedCharacter = c;

    await crawler.PersistCharacterAsync(42);

    Assert.NotNull(addedCharacter);
    Assert.Equal(42, addedCharacter.ComicVineId);
  }

  [Fact]
  public async Task PersistCharacterAsync_NeverRaisesIssueConnectionConfirmedEvent()
  {
    const int newCharacterId = 42;
    const int alreadyKnownId = 999;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [newCharacterId] = Character(newCharacterId, "New", issues: [700])
    };
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(alreadyKnownId, "AlreadyKnown"));
    await graphStore.UpsertCharacterIssueCreditsAsync(alreadyKnownId, [700]);
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = new ConnectionCrawler(characterSource, graphStore);
    var raised = false;
    crawler.IssueConnectionConfirmed += _ => raised = true;

    await crawler.PersistCharacterAsync(newCharacterId);

    Assert.False(raised);
  }
}
