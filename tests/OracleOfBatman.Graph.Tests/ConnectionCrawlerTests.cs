using Microsoft.Extensions.Time.Testing;
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

  private static ComicVineIssue Issue(int id, params int[] cast) => new()
  {
    Id = id,
    Name = $"Issue{id}",
    Image = new ComicVineImage { IconUrl = $"https://example.com/issue{id}.jpg" },
    CharacterCredits = [.. cast.Select(c => new ComicVineCharacterRef { Id = c, Name = $"Character{c}" })]
  };

  private static ConnectionCrawler Crawler(FakeComicVineCharacterSource characterSource, FakeGraphStore graphStore,
    FakeComicVineIssueSource? issueSource = null, TimeProvider? timeProvider = null) =>
    new(characterSource, graphStore,
      new IssueEnrichmentService(issueSource ?? new FakeComicVineIssueSource([]), graphStore), timeProvider);

  [Fact]
  public async Task AlreadyConnected_ReturnsImmediatelyWithoutFetchingAnything()
  {
    // ADR-0016: the seeds must already share a CREDITED_IN-connected Issue.
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(SeedA, "A"));
    await graphStore.UpsertCreditedInAsync(SeedA, [new Issue(999, "Issue999")]);
    await graphStore.UpsertCharacterAsync(new Character(SeedB, "B"));
    await graphStore.UpsertCreditedInAsync(SeedB, [new Issue(999, "Issue999")]);
    var characterSource = new FakeComicVineCharacterSource([]);
    var crawler = Crawler(characterSource, graphStore);

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
    var crawler = Crawler(characterSource, graphStore);

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.True(result.Connected);
    Assert.NotNull(await graphStore.GetIssueAsync(500));
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
    var crawler = Crawler(characterSource, graphStore);

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.True(result.Connected);
    Assert.Equal(1, result.CharactersFetched);
    Assert.NotNull(await graphStore.GetIssueAsync(100));
    Assert.NotNull(await graphStore.GetIssueAsync(200));
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
    var crawler = Crawler(characterSource, graphStore);

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
    var crawler = Crawler(characterSource, graphStore);

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
    var crawler = Crawler(characterSource, graphStore);

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
    await graphStore.UpsertCreditedInAsync(earlierRunCharacter, [new Issue(555, "Issue555")]);
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = Crawler(characterSource, graphStore);

    await crawler.PopulateConnectionsAsync(SeedA, SeedB, 1);

    Assert.DoesNotContain(earlierRunCharacter, characterSource.FetchedIds);
    Assert.NotNull(await graphStore.GetIssueAsync(555));
    Assert.True(await graphStore.PathExistsAsync(fromA, earlierRunCharacter));
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
    // Nobody outside the three characters is credited anywhere, so the issue-cast
    // escalation that follows the exhausted BFS finds no candidate to fetch.
    var issues = new Dictionary<int, ComicVineIssue>
    {
      [111] = Issue(111, SeedA),
      [222] = Issue(222, SeedB),
      [333] = Issue(333, mutualFriend)
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = Crawler(characterSource, graphStore, new FakeComicVineIssueSource(issues));

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
    await graphStore.UpsertCreditedInAsync(alreadyKnownId, [new Issue(700, "Issue700")]);
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = Crawler(characterSource, graphStore);

    var ingested = await crawler.IngestCharacterAsync(newCharacterId);

    Assert.Equal("New", ingested.Name);
    Assert.Contains(graphStore.Characters, c => c.ComicVineId == newCharacterId);
    Assert.NotNull(await graphStore.GetIssueAsync(700));
    Assert.True(await graphStore.PathExistsAsync(newCharacterId, alreadyKnownId));
  }

  [Fact]
  public async Task IngestCharacterAsync_RaisesCharacterAddedEvent_WhenTheCharacterIsNewToTheGraph()
  {
    var characters = new Dictionary<int, ComicVineCharacter> { [42] = Character(42, "New") };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = Crawler(characterSource, graphStore);
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
    var crawler = Crawler(characterSource, graphStore);
    var raised = false;
    crawler.CharacterAdded += _ => raised = true;

    await crawler.IngestCharacterAsync(existingId);

    Assert.False(raised);
  }


  [Fact]
  public async Task IngestCharacterAsync_PersistsFriendAndEnemyIds()
  {
    // Free on the same Comic Vine response (ADR-0016) — discovery-only, never a path segment.
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [42] = Character(42, "New", friends: [10, 20], enemies: [30])
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = Crawler(characterSource, graphStore);

    await crawler.IngestCharacterAsync(42);

    var found = await graphStore.GetCharacterAsync(42);
    Assert.Equal([10, 20], found!.FriendIds);
    Assert.Equal([30], found.EnemyIds);
  }

  [Fact]
  public async Task IngestCharacterAsync_StampsIngestionDateTime_UsingTheInjectedTimeProvider()
  {
    var characters = new Dictionary<int, ComicVineCharacter> { [42] = Character(42, "New") };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
    var crawler = Crawler(characterSource, graphStore, timeProvider: timeProvider);

    await crawler.IngestCharacterAsync(42);

    var found = await graphStore.GetCharacterAsync(42);
    Assert.Equal(new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc), found!.IngestionDateTime);
  }

  [Fact]
  public async Task IngestCharacterAsync_UpdatesIngestionDateTime_OnReIngest()
  {
    // The Random Character feature (ADR-0016) depends on re-ingesting a seed bumping this
    // timestamp forward, not just setting it once on first ingest.
    var characters = new Dictionary<int, ComicVineCharacter> { [42] = Character(42, "New") };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
    var crawler = Crawler(characterSource, graphStore, timeProvider: timeProvider);
    await crawler.IngestCharacterAsync(42);

    timeProvider.SetUtcNow(new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero));
    await crawler.IngestCharacterAsync(42);

    var found = await graphStore.GetCharacterAsync(42);
    Assert.Equal(new DateTime(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc), found!.IngestionDateTime);
  }

  [Fact]
  public async Task PickRandomCharacterAsync_ReturnsLeastRecentlyIngestedCharacter()
  {
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(1, "A",
      ingestionDateTime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    await graphStore.UpsertCharacterAsync(new Character(2, "B",
      ingestionDateTime: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
    var crawler = Crawler(new FakeComicVineCharacterSource([]), graphStore);

    var picked = await crawler.PickRandomCharacterAsync(excludeCharacterId: null);

    Assert.Equal(1, picked!.ComicVineId);
  }

  [Fact]
  public async Task PickRandomCharacterAsync_ExcludesTheOtherSlotsCharacter()
  {
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(1, "A",
      ingestionDateTime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    await graphStore.UpsertCharacterAsync(new Character(2, "B",
      ingestionDateTime: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
    var crawler = Crawler(new FakeComicVineCharacterSource([]), graphStore);

    var picked = await crawler.PickRandomCharacterAsync(excludeCharacterId: 1);

    Assert.Equal(2, picked!.ComicVineId);
  }

  [Fact]
  public async Task PickRandomCharacterAsync_ExcludesPreviouslyShownCharacters_AcrossMultipleCalls()
  {
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(1, "A",
      ingestionDateTime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    await graphStore.UpsertCharacterAsync(new Character(2, "B",
      ingestionDateTime: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
    await graphStore.UpsertCharacterAsync(new Character(3, "C",
      ingestionDateTime: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
    var crawler = Crawler(new FakeComicVineCharacterSource([]), graphStore);

    var first = await crawler.PickRandomCharacterAsync(excludeCharacterId: null);
    var second = await crawler.PickRandomCharacterAsync(excludeCharacterId: null);

    Assert.Equal(1, first!.ComicVineId);
    Assert.Equal(2, second!.ComicVineId);
  }

  [Fact]
  public async Task PickRandomCharacterAsync_ResetsShownSet_OnceEveryCandidateHasBeenShown()
  {
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertCharacterAsync(new Character(1, "A",
      ingestionDateTime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    await graphStore.UpsertCharacterAsync(new Character(2, "B",
      ingestionDateTime: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
    var crawler = Crawler(new FakeComicVineCharacterSource([]), graphStore);

    var first = await crawler.PickRandomCharacterAsync(excludeCharacterId: null);
    var second = await crawler.PickRandomCharacterAsync(excludeCharacterId: null);
    var third = await crawler.PickRandomCharacterAsync(excludeCharacterId: null);

    Assert.Equal(1, first!.ComicVineId);
    Assert.Equal(2, second!.ComicVineId);
    Assert.Equal(1, third!.ComicVineId);
  }

  [Fact]
  public async Task FriendsAndEnemiesExhausted_EscalatesToIssueCasts_AndIngestsACharacterCreditedInTwoOfThem()
  {
    // ADR-0014 steps 4-5: neither seed has a friend or enemy to follow, so the only way
    // to find a bridge is the full cast of the issues they're credited in.
    const int bridge = 30;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", issues: [100]),
      [SeedB] = Character(SeedB, "B", issues: [200]),
      [bridge] = Character(bridge, "Bridge", issues: [100, 200])
    };
    var issues = new Dictionary<int, ComicVineIssue>
    {
      [100] = Issue(100, SeedA, bridge),
      [200] = Issue(200, SeedB, bridge)
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var issueSource = new FakeComicVineIssueSource(issues);
    var crawler = Crawler(characterSource, graphStore, issueSource);

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.True(result.Connected);
    Assert.Equal([SeedA, SeedB, bridge], characterSource.FetchedIds);
    Assert.Equal(1, result.CharactersFetched);
    Assert.Equal(2, result.IssuesFetched);
  }

  [Fact]
  public async Task IssueCastEscalation_IgnoresCharactersCreditedInOnlyOneOfTheFetchedCasts()
  {
    // The strong-candidate rule (ADR-0014 step 5): a single shared cast isn't worth a
    // request, or a large ensemble issue would drain the whole budget by itself.
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", issues: [100]),
      [SeedB] = Character(SeedB, "B", issues: [200])
    };
    var issues = new Dictionary<int, ComicVineIssue>
    {
      [100] = Issue(100, SeedA, 40),
      [200] = Issue(200, SeedB, 41)
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = Crawler(characterSource, graphStore, new FakeComicVineIssueSource(issues));

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.False(result.Connected);
    Assert.Equal([SeedA, SeedB], characterSource.FetchedIds);
  }

  [Fact]
  public async Task IssueCastEscalation_ReusesAlreadyMaterializedCharacterCredits_WithoutRefetchingTheIssue()
  {
    // ADR-0015's refinement of ADR-0014 step 4: a cast already in Neo4j (from some
    // earlier, unrelated enrichment) is free — never pay Comic Vine for it twice.
    const int bridge = 30;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", issues: [100]),
      [SeedB] = Character(SeedB, "B", issues: [200]),
      [bridge] = Character(bridge, "Bridge", issues: [100, 200])
    };
    var graphStore = new FakeGraphStore();
    await graphStore.UpsertIssueAsync(new Issue(100, "Issue100", characterCredits: [SeedA, bridge]));
    await graphStore.UpsertIssueAsync(new Issue(200, "Issue200", characterCredits: [SeedB, bridge]));
    var characterSource = new FakeComicVineCharacterSource(characters);
    // Empty: any real fetch attempt throws, failing the test loudly.
    var issueSource = new FakeComicVineIssueSource([]);
    var crawler = Crawler(characterSource, graphStore, issueSource);

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.True(result.Connected);
    Assert.Empty(issueSource.FetchedIds);
    Assert.Equal(0, result.IssuesFetched);
  }

  [Fact]
  public async Task IssueCastEscalation_CountsIssueFetchesAgainstTheSameBudgetAsCharacterFetches()
  {
    // ADR-0014's one unified counter: an /issue/{id}/ cast-fetch costs exactly what a
    // /character/{id}/ fetch costs, even though it reveals many candidates at once.
    const int bridge = 30;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", issues: [100]),
      [SeedB] = Character(SeedB, "B", issues: [200]),
      [bridge] = Character(bridge, "Bridge", issues: [100, 200])
    };
    var issues = new Dictionary<int, ComicVineIssue>
    {
      [100] = Issue(100, SeedA, bridge),
      [200] = Issue(200, SeedB, bridge)
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = Crawler(characterSource, graphStore, new FakeComicVineIssueSource(issues));

    // Only enough budget for one cast fetch — never enough to see the bridge in two of
    // them, let alone ingest it.
    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 1);

    Assert.False(result.Connected);
    Assert.Equal(1, result.IssuesFetched);
    Assert.Equal(0, result.CharactersFetched);
    Assert.DoesNotContain(bridge, characterSource.FetchedIds);
  }

  [Fact]
  public async Task IssueCastEscalation_RecursesIntoAStrongCandidatesOwnIssues()
  {
    // ADR-0014 step 5: a newly-ingested strong candidate's issues become the next
    // round's candidate pool, so escalation can walk more than one issue deep.
    const int firstBridge = 30;
    const int secondBridge = 31;
    var characters = new Dictionary<int, ComicVineCharacter>
    {
      [SeedA] = Character(SeedA, "A", issues: [100, 101]),
      [SeedB] = Character(SeedB, "B", issues: [200]),
      // Shares no issue with SeedB — only with SeedA and with secondBridge.
      [firstBridge] = Character(firstBridge, "FirstBridge", issues: [100, 101, 300, 301]),
      [secondBridge] = Character(secondBridge, "SecondBridge", issues: [300, 301, 200])
    };
    var issues = new Dictionary<int, ComicVineIssue>
    {
      [100] = Issue(100, SeedA, firstBridge),
      [101] = Issue(101, SeedA, firstBridge),
      [200] = Issue(200, SeedB, secondBridge),
      // Only reachable once firstBridge has been ingested and its own credits pooled.
      [300] = Issue(300, firstBridge, secondBridge),
      [301] = Issue(301, firstBridge, secondBridge)
    };
    var graphStore = new FakeGraphStore();
    var characterSource = new FakeComicVineCharacterSource(characters);
    var crawler = Crawler(characterSource, graphStore, new FakeComicVineIssueSource(issues));

    var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, 10);

    Assert.True(result.Connected);
    Assert.Contains(secondBridge, characterSource.FetchedIds);
  }
}
