using OracleOfBatman.Domain;

namespace OracleOfBatman.Graph.Tests;

/// <summary>
///   IGraphStore's behavioral contract (ADR-0016), written once and run against every
///   implementation via CreateStore() — FakeGraphStoreContractTests (instant, in-memory) and
///   Neo4jGraphWriterContractTests (real Neo4j, one shared Testcontainers instance for the
///   whole class — see Neo4jContainerFixture). One definition, both stores verified, no drift
///   between a fast fake and the real thing. Implementation-specific behavior (raw Cypher wire
///   format, non-interface methods like GetSummaryAsync) stays in the concrete test classes
///   instead of here.
///
///   Every test generates its own ids via NextId() rather than hardcoding literals — the Neo4j
///   subclass shares one database across its whole test run, so reused literal ids would let
///   unrelated tests collide with each other's data.
/// </summary>
public abstract class GraphStoreContractTests
{
  private static int _nextId = 1_000_000;

  protected abstract IGraphStore CreateStore();

  protected static int NextId() => Interlocked.Increment(ref _nextId);

  [Fact]
  public async Task GetCharacterAsync_ReturnsNullWhenNotYetPersisted()
  {
    var store = CreateStore();

    var found = await store.GetCharacterAsync(NextId());

    Assert.Null(found);
  }

  [Fact]
  public async Task GetCharacterAsync_ReturnsThePersistedCharacter()
  {
    var store = CreateStore();
    var characterId = NextId();
    var batman = new Character(characterId, "Batman", imageUrl: "https://example.com/batman.jpg",
      siteDetailUrl: "https://comicvine.gamespot.com/batman/4005-1699/");
    await store.UpsertCharacterAsync(batman);

    var found = await store.GetCharacterAsync(characterId);

    Assert.Equal(batman, found);
  }

  [Fact]
  public async Task UpsertCharacterAsync_PersistsFriendAndEnemyIds()
  {
    // Free on the same Comic Vine response (ADR-0016) — discovery-only, never a path
    // segment. The friend/enemy ids themselves are just raw ints on the array property,
    // not node keys, so they don't need to be unique — only the character's own id does.
    var store = CreateStore();
    var characterId = NextId();
    var character = new Character(characterId, "A", friendIds: [10, 20], enemyIds: [30]);

    await store.UpsertCharacterAsync(character);

    var found = await store.GetCharacterAsync(characterId);
    Assert.Equal([10, 20], found!.FriendIds);
    Assert.Equal([30], found.EnemyIds);
  }

  [Fact]
  public async Task UpsertCharacterAsync_PersistsIngestionDateTime()
  {
    var store = CreateStore();
    var characterId = NextId();
    var ingestionDateTime = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
    var character = new Character(characterId, "A", ingestionDateTime: ingestionDateTime);

    await store.UpsertCharacterAsync(character);

    var found = await store.GetCharacterAsync(characterId);
    Assert.Equal(ingestionDateTime, found!.IngestionDateTime);
  }

  [Fact]
  public async Task GetIssueAsync_ReturnsNullWhenNeverCredited()
  {
    // No overlap confirmation needed anymore (ADR-0016) — but a Comic Vine issue id
    // nobody has ever been credited on still has no node at all.
    var store = CreateStore();

    var found = await store.GetIssueAsync(NextId());

    Assert.Null(found);
  }

  [Fact]
  public async Task UpsertCreditedInAsync_MaterializesAnIssueStub_ForASingleCharactersOwnCredit()
  {
    // ADR-0016: unlike ADR-0015, no second Character or overlap is required — the
    // Issue stub materializes the moment anyone is credited on it.
    var store = CreateStore();
    var characterId = NextId();
    var issueId = NextId();
    await store.UpsertCharacterAsync(new Character(characterId, "A"));

    await store.UpsertCreditedInAsync(characterId, [new Issue(issueId, "Some Issue")]);

    var found = await store.GetIssueAsync(issueId);
    Assert.NotNull(found);
    Assert.Equal(issueId, found.ComicVineId);
  }

  [Fact]
  public async Task UpsertCreditedInAsync_SetsIssueNameAndSiteDetailUrl_FromTheCredit()
  {
    // Free on the same Character response (ADR-0016) — no separate Comic Vine request.
    var store = CreateStore();
    var characterId = NextId();
    var issueId = NextId();
    await store.UpsertCharacterAsync(new Character(characterId, "A"));

    await store.UpsertCreditedInAsync(characterId,
      [new Issue(issueId, "Some Issue", siteDetailUrl: "https://comicvine.gamespot.com/some-issue/4000-1101757/")]);

    var found = await store.GetIssueAsync(issueId);
    Assert.Equal("Some Issue", found!.Name);
    Assert.Equal("https://comicvine.gamespot.com/some-issue/4000-1101757/", found.SiteDetailUrl);
  }

  [Fact]
  public async Task UpsertCreditedInAsync_IsIdempotent_WhenCalledTwiceForTheSameCredit()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    var issueId = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));

    await store.UpsertCreditedInAsync(characterAId, [new Issue(issueId, "Some Issue")]);
    await store.UpsertCreditedInAsync(characterAId, [new Issue(issueId, "Some Issue")]);
    await store.UpsertCreditedInAsync(characterBId, [new Issue(issueId, "Some Issue")]);

    var pathExists = await store.PathExistsAsync(characterAId, characterBId);
    Assert.True(pathExists);
  }

  [Fact]
  public async Task UpsertCreditedInAsync_DoesNotOverwriteAlreadyEnrichedIssueData()
  {
    // A later, plainer credit for the same Issue (e.g. re-ingesting a Character) must
    // not clobber enrichment data an earlier IssueEnrichmentService pass already wrote.
    var store = CreateStore();
    var characterId = NextId();
    var issueId = NextId();
    await store.UpsertCharacterAsync(new Character(characterId, "A"));
    await store.UpsertCreditedInAsync(characterId, [new Issue(issueId, "Some Issue")]);
    await store.UpsertIssueAsync(new Issue(issueId, "Some Issue", imageUrl: "https://example.com/cover.jpg"));

    await store.UpsertCreditedInAsync(characterId, [new Issue(issueId, "Some Issue")]);

    var found = await store.GetIssueAsync(issueId);
    Assert.Equal("https://example.com/cover.jpg", found!.ImageUrl);
  }

  [Fact]
  public async Task UpsertIssueAsync_PersistsNameImageVolumeAndSiteDetailUrl()
  {
    // ADR-0015 Slice 6: the unified enrichment fetch's write-back step.
    var store = CreateStore();
    var issueId = NextId();
    var issue = new Issue(issueId, "Some Issue", imageUrl: "https://example.com/cover.jpg",
      siteDetailUrl: "https://comicvine.gamespot.com/some-issue/4000-500/", volumeId: 9, volumeName: "The Volume Title");

    await store.UpsertIssueAsync(issue);

    var found = await store.GetIssueAsync(issueId);
    Assert.Equal(issue, found);
  }

  [Fact]
  public async Task UpsertIssueAsync_PersistsCharacterCredits()
  {
    // The raw Comic Vine cast list (ADR-0016) — ids only, ingested-or-not, used for
    // crawl-frontier discovery rather than pathfinding.
    var store = CreateStore();
    var issueId = NextId();
    var issue = new Issue(issueId, "Some Issue", characterCredits: [10, 20]);

    await store.UpsertIssueAsync(issue);

    var found = await store.GetIssueAsync(issueId);
    Assert.Equal([10, 20], found!.CharacterCredits);
  }

  [Fact]
  public async Task PathExistsAsync_FalseWhenCharactersAreUnconnected()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));

    var pathExists = await store.PathExistsAsync(characterAId, characterBId);

    Assert.False(pathExists);
  }

  [Fact]
  public async Task PathExistsAsync_FalseWhenEitherCharacterIsNotYetInTheGraph()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var neverPersistedId = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));

    var pathExists = await store.PathExistsAsync(characterAId, neverPersistedId);

    Assert.False(pathExists);
  }

  [Fact]
  public async Task PathExistsAsync_TrueViaSharedCreditedInIssue()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    var issueId = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCreditedInAsync(characterAId, [new Issue(issueId, "Some Issue")]);
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));
    await store.UpsertCreditedInAsync(characterBId, [new Issue(issueId, "Some Issue")]);

    var pathExists = await store.PathExistsAsync(characterAId, characterBId);

    Assert.True(pathExists);
  }

  [Fact]
  public async Task PathExistsAsync_TrueViaMultiHopThroughSeparateCreditedInIssues()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    var characterCId = NextId();
    var issue1Id = NextId();
    var issue2Id = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCreditedInAsync(characterAId, [new Issue(issue1Id, "Issue 1")]);
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));
    await store.UpsertCreditedInAsync(characterBId, [new Issue(issue1Id, "Issue 1"), new Issue(issue2Id, "Issue 2")]);
    await store.UpsertCharacterAsync(new Character(characterCId, "C"));
    await store.UpsertCreditedInAsync(characterCId, [new Issue(issue2Id, "Issue 2")]);

    var pathExists = await store.PathExistsAsync(characterAId, characterCId);

    Assert.True(pathExists);
  }

  [Fact]
  public async Task FindShortestPathAsync_ReturnsNull_WhenEitherCharacterDoesNotExist()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var neverPersistedId = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));

    var path = await store.FindShortestPathAsync(characterAId, neverPersistedId, 5);

    Assert.Null(path);
  }

  [Fact]
  public async Task FindShortestPathAsync_ReturnsNull_WhenNoPathExists()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));

    var path = await store.FindShortestPathAsync(characterAId, characterBId, 5);

    Assert.Null(path);
  }

  [Fact]
  public async Task FindShortestPathAsync_ReturnsOneHopPath_ViaSharedCreditedInIssue()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    var issueId = NextId();
    var characterA = new Character(characterAId, "A");
    var characterB = new Character(characterBId, "B");
    await store.UpsertCharacterAsync(characterA);
    await store.UpsertCreditedInAsync(characterAId, [new Issue(issueId, "Some Issue")]);
    await store.UpsertCharacterAsync(characterB);
    await store.UpsertCreditedInAsync(characterBId, [new Issue(issueId, "Some Issue")]);

    var path = await store.FindShortestPathAsync(characterAId, characterBId, 5);

    Assert.NotNull(path);
    Assert.Equal(1, path.BatmanNumber);
    Assert.Equal([characterA, characterB], path.Characters);
    var hop = Assert.Single(path.Hops);
    Assert.Equal(characterA, hop.From);
    Assert.Equal(characterB, hop.To);
    Assert.Equal(issueId, hop.Issue.ComicVineId);
    Assert.Equal("Some Issue", hop.Issue.Name);
  }

  [Fact]
  public async Task FindShortestPathAsync_ReturnsHopsInWalkOrder_ThroughSeparateCreditedInIssues()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    var characterCId = NextId();
    var issue1Id = NextId();
    var issue2Id = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCreditedInAsync(characterAId, [new Issue(issue1Id, "Issue 1")]);
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));
    await store.UpsertCreditedInAsync(characterBId, [new Issue(issue1Id, "Issue 1"), new Issue(issue2Id, "Issue 2")]);
    await store.UpsertCharacterAsync(new Character(characterCId, "C"));
    await store.UpsertCreditedInAsync(characterCId, [new Issue(issue2Id, "Issue 2")]);

    var path = await store.FindShortestPathAsync(characterAId, characterCId, 5);

    Assert.NotNull(path);
    // Comparing ids, not full record equality — B is an intermediate Character, so
    // FindShortestPathAsync's own BridgeUseCount bump (ADR-0016 Slice 5) mutates the
    // persisted record between construction above and read-back here; only walk order
    // is this test's actual concern.
    Assert.Equal([characterAId, characterBId, characterCId], path.Characters.Select(c => c.ComicVineId));
    Assert.Equal(2, path.Hops.Count);
    Assert.Equal(characterAId, path.Hops[0].From.ComicVineId);
    Assert.Equal(characterBId, path.Hops[0].To.ComicVineId);
    Assert.Equal(characterBId, path.Hops[1].From.ComicVineId);
    Assert.Equal(characterCId, path.Hops[1].To.ComicVineId);
  }

  [Fact]
  public async Task FindShortestPathAsync_ReturnsNull_WhenShortestPathExceedsMaxDepth()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    var characterCId = NextId();
    var issue1Id = NextId();
    var issue2Id = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCreditedInAsync(characterAId, [new Issue(issue1Id, "Issue 1")]);
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));
    await store.UpsertCreditedInAsync(characterBId, [new Issue(issue1Id, "Issue 1"), new Issue(issue2Id, "Issue 2")]);
    await store.UpsertCharacterAsync(new Character(characterCId, "C"));
    await store.UpsertCreditedInAsync(characterCId, [new Issue(issue2Id, "Issue 2")]);

    var path = await store.FindShortestPathAsync(characterAId, characterCId, 1);

    Assert.Null(path);
  }

  [Fact]
  public async Task FindShortestPathAsync_IncrementsBridgeUseCount_ForIntermediateCharactersOnly()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    var characterCId = NextId();
    var issue1Id = NextId();
    var issue2Id = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCreditedInAsync(characterAId, [new Issue(issue1Id, "Issue 1")]);
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));
    await store.UpsertCreditedInAsync(characterBId, [new Issue(issue1Id, "Issue 1"), new Issue(issue2Id, "Issue 2")]);
    await store.UpsertCharacterAsync(new Character(characterCId, "C"));
    await store.UpsertCreditedInAsync(characterCId, [new Issue(issue2Id, "Issue 2")]);

    await store.FindShortestPathAsync(characterAId, characterCId, 5);

    Assert.Equal(0, (await store.GetCharacterAsync(characterAId))!.BridgeUseCount);
    Assert.Equal(1, (await store.GetCharacterAsync(characterBId))!.BridgeUseCount);
    Assert.Equal(0, (await store.GetCharacterAsync(characterCId))!.BridgeUseCount);
  }

  [Fact]
  public async Task FindShortestPathAsync_IncrementsPathUseCount_ForEveryHopIssue()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    var characterCId = NextId();
    var issue1Id = NextId();
    var issue2Id = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCreditedInAsync(characterAId, [new Issue(issue1Id, "Issue 1")]);
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));
    await store.UpsertCreditedInAsync(characterBId, [new Issue(issue1Id, "Issue 1"), new Issue(issue2Id, "Issue 2")]);
    await store.UpsertCharacterAsync(new Character(characterCId, "C"));
    await store.UpsertCreditedInAsync(characterCId, [new Issue(issue2Id, "Issue 2")]);

    await store.FindShortestPathAsync(characterAId, characterCId, 5);

    Assert.Equal(1, (await store.GetIssueAsync(issue1Id))!.PathUseCount);
    Assert.Equal(1, (await store.GetIssueAsync(issue2Id))!.PathUseCount);
  }

  [Fact]
  public async Task FindShortestPathAsync_DoesNotIncrementCounts_WhenNoPathFound()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    var issueId = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCreditedInAsync(characterAId, [new Issue(issueId, "Issue 1")]);
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));
    await store.UpsertCreditedInAsync(characterBId, [new Issue(NextId(), "Issue 2")]);

    await store.FindShortestPathAsync(characterAId, characterBId, 5);

    Assert.Equal(0, (await store.GetCharacterAsync(characterAId))!.BridgeUseCount);
    Assert.Equal(0, (await store.GetIssueAsync(issueId))!.PathUseCount);
  }

  [Fact]
  public async Task FindShortestPathAsync_AccumulatesBridgeUseCount_AcrossRepeatedSuccessfulCalls()
  {
    // Every successful call counts, including a repeat lookup of an already-known path
    // (ADR-0016) — not gated behind "was this freshly discovered."
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    var characterCId = NextId();
    var issue1Id = NextId();
    var issue2Id = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCreditedInAsync(characterAId, [new Issue(issue1Id, "Issue 1")]);
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));
    await store.UpsertCreditedInAsync(characterBId, [new Issue(issue1Id, "Issue 1"), new Issue(issue2Id, "Issue 2")]);
    await store.UpsertCharacterAsync(new Character(characterCId, "C"));
    await store.UpsertCreditedInAsync(characterCId, [new Issue(issue2Id, "Issue 2")]);

    await store.FindShortestPathAsync(characterAId, characterCId, 5);
    await store.FindShortestPathAsync(characterAId, characterCId, 5);

    Assert.Equal(2, (await store.GetCharacterAsync(characterBId))!.BridgeUseCount);
  }

  [Fact]
  public async Task RecordSeedUseAsync_IncrementsSeedUseCountForBothCharacters()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));

    await store.RecordSeedUseAsync(characterAId, characterBId);

    Assert.Equal(1, (await store.GetCharacterAsync(characterAId))!.SeedUseCount);
    Assert.Equal(1, (await store.GetCharacterAsync(characterBId))!.SeedUseCount);
  }

  [Fact]
  public async Task RecordSeedUseAsync_AccumulatesAcrossRepeatedCalls()
  {
    var store = CreateStore();
    var characterAId = NextId();
    var characterBId = NextId();
    await store.UpsertCharacterAsync(new Character(characterAId, "A"));
    await store.UpsertCharacterAsync(new Character(characterBId, "B"));

    await store.RecordSeedUseAsync(characterAId, characterBId);
    await store.RecordSeedUseAsync(characterAId, characterBId);

    Assert.Equal(2, (await store.GetCharacterAsync(characterAId))!.SeedUseCount);
  }
}
