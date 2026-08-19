using OracleOfBatman.Domain;

namespace OracleOfBatman.Graph.Tests.Fakes;

/// <summary>
///   Runs the shared IGraphStore contract (GraphStoreContractTests) against FakeGraphStore —
///   instant, in-memory, so ConnectionCrawlerTests can trust the Fake's decision logic without
///   needing Docker.
/// </summary>
public sealed class FakeGraphStoreContractTests : GraphStoreContractTests
{
  protected override IGraphStore CreateStore() => new FakeGraphStore();

  // GetLeastRecentlyIngestedCharacterAsync is a whole-graph scan by nature (ADR-0016) — it
  // doesn't fit the shared contract's per-test-unique-id isolation the way every other method
  // does, since "the oldest excluding these ids" can't be made deterministic on a store shared
  // across many tests (Neo4jGraphWriterContractTests) without knowing every other test's data.
  // FakeGraphStore gets a genuinely fresh, empty store per test, so it's the only place these
  // run — Neo4jGraphWriterContractTests covers the Cypher translation separately with its own
  // explicit graph reset.

  [Fact]
  public async Task GetLeastRecentlyIngestedCharacterAsync_ReturnsTheOldestIngestedCharacter()
  {
    var store = CreateStore();
    var older = new Character(1, "A", ingestionDateTime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    var newer = new Character(2, "B", ingestionDateTime: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
    await store.UpsertCharacterAsync(older);
    await store.UpsertCharacterAsync(newer);

    var found = await store.GetLeastRecentlyIngestedCharacterAsync([]);

    Assert.Equal(1, found!.ComicVineId);
  }

  [Fact]
  public async Task GetLeastRecentlyIngestedCharacterAsync_ExcludesGivenIds()
  {
    var store = CreateStore();
    var older = new Character(1, "A", ingestionDateTime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    var newer = new Character(2, "B", ingestionDateTime: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
    await store.UpsertCharacterAsync(older);
    await store.UpsertCharacterAsync(newer);

    var found = await store.GetLeastRecentlyIngestedCharacterAsync([1]);

    Assert.Equal(2, found!.ComicVineId);
  }

  [Fact]
  public async Task GetLeastRecentlyIngestedCharacterAsync_ReturnsNull_WhenAllCandidatesExcluded()
  {
    var store = CreateStore();
    await store.UpsertCharacterAsync(new Character(1, "A", ingestionDateTime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    await store.UpsertCharacterAsync(new Character(2, "B", ingestionDateTime: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

    var found = await store.GetLeastRecentlyIngestedCharacterAsync([1, 2]);

    Assert.Null(found);
  }

  [Fact]
  public async Task GetLeastRecentlyIngestedCharacterAsync_IgnoresCharactersWithNoIngestionDateTime()
  {
    // Never actually ingested — nothing to refresh, so never a candidate even though a
    // null date would otherwise sort as "oldest" if it were treated as a real value.
    var store = CreateStore();
    await store.UpsertCharacterAsync(new Character(1, "A"));
    var actuallyIngested = new Character(2, "B", ingestionDateTime: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
    await store.UpsertCharacterAsync(actuallyIngested);

    var found = await store.GetLeastRecentlyIngestedCharacterAsync([]);

    Assert.Equal(2, found!.ComicVineId);
  }
}
