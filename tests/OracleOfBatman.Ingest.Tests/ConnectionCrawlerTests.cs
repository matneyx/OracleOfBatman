using OracleOfBatman.Ingest.ComicVine;
using OracleOfBatman.Ingest.Tests.Fakes;
using Xunit;

namespace OracleOfBatman.Ingest.Tests;

/// <summary>Tests the ADR-0010 crawl algorithm's decision logic against in-memory fakes —
/// no Docker/HTTP needed, so these run as fast unit tests.</summary>
public class ConnectionCrawlerTests
{
    private const int SeedA = 1;
    private const int SeedB = 2;

    private static ComicVineCharacter Character(int id, string name, IEnumerable<int>? friends = null, IEnumerable<int>? enemies = null, IEnumerable<int>? issues = null) => new()
    {
        Id = id,
        Name = name,
        CharacterFriends = (friends ?? []).Select(f => new ComicVineCharacterRef { Id = f, Name = $"Character{f}" }).ToList(),
        CharacterEnemies = (enemies ?? []).Select(e => new ComicVineCharacterRef { Id = e, Name = $"Character{e}" }).ToList(),
        IssueCredits = (issues ?? []).Select(i => new ComicVineIssueRef { Id = i }).ToList(),
    };

    [Fact]
    public async Task AlreadyConnected_ReturnsImmediatelyWithoutFetchingAnything()
    {
        var graphStore = new FakeGraphStore();
        await graphStore.UpsertCharacterAsync(new(SeedA, "A"));
        await graphStore.UpsertCharacterAsync(new(SeedB, "B"));
        await graphStore.UpsertConnectionAsync(new(SeedA, SeedB, 999, null, Domain.InteractionTier.SharedScene, Domain.Confidence.Unverified));
        var characterSource = new FakeComicVineCharacterSource([]);
        var crawler = new ConnectionCrawler(characterSource, graphStore);

        var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, budget: 10);

        Assert.True(result.Connected);
        Assert.Equal(0, result.CharactersFetched);
        Assert.Empty(characterSource.FetchedIds);
    }

    [Fact]
    public async Task DirectIssueOverlapBetweenSeeds_CreatesConnectionAndStopsWithoutExpanding()
    {
        var characters = new Dictionary<int, ComicVineCharacter>
        {
            [SeedA] = Character(SeedA, "A", friends: [10], issues: [500]),
            [SeedB] = Character(SeedB, "B", friends: [20], issues: [500]),
        };
        var graphStore = new FakeGraphStore();
        var characterSource = new FakeComicVineCharacterSource(characters);
        var crawler = new ConnectionCrawler(characterSource, graphStore);

        var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, budget: 10);

        Assert.True(result.Connected);
        Assert.Contains(graphStore.Connections, c => c.ComicIssueId == 500);
        Assert.Equal([SeedA, SeedB], characterSource.FetchedIds);
    }

    [Fact]
    public async Task DirectFriendOverlap_FetchesSharedCharacterAndConnectsBothSeedsThroughIt()
    {
        const int sharedFriend = 30;
        var characters = new Dictionary<int, ComicVineCharacter>
        {
            [SeedA] = Character(SeedA, "A", friends: [sharedFriend], issues: [100]),
            [SeedB] = Character(SeedB, "B", friends: [sharedFriend], issues: [200]),
            [sharedFriend] = Character(sharedFriend, "Shared", issues: [100, 200]),
        };
        var graphStore = new FakeGraphStore();
        var characterSource = new FakeComicVineCharacterSource(characters);
        var crawler = new ConnectionCrawler(characterSource, graphStore);

        var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, budget: 10);

        Assert.True(result.Connected);
        Assert.Equal(1, result.CharactersFetched);
        Assert.Contains(graphStore.Connections, c => c.ComicIssueId == 100);
        Assert.Contains(graphStore.Connections, c => c.ComicIssueId == 200);
        Assert.Equal([SeedA, SeedB, sharedFriend], characterSource.FetchedIds);
    }

    [Fact]
    public async Task NoOverlapAtAll_BidirectionalBfsChecksNewCharactersAgainstFullAccumulatedSet()
    {
        const int fromA = 10;
        const int fromB = 20;
        var characters = new Dictionary<int, ComicVineCharacter>
        {
            [SeedA] = Character(SeedA, "A", friends: [fromA], issues: [111]),
            // fromA bridges A (issue 111) to fromB (issue 999) — a real 3-hop chain, not a
            // dead end: A-fromA share 111, fromA-fromB share 999, fromB-B share 222.
            [fromA] = Character(fromA, "FromA", issues: [111, 999]),
            [fromB] = Character(fromB, "FromB", issues: [999, 222]),
            [SeedB] = Character(SeedB, "B", friends: [fromB], issues: [222]),
        };
        var graphStore = new FakeGraphStore();
        var characterSource = new FakeComicVineCharacterSource(characters);
        var crawler = new ConnectionCrawler(characterSource, graphStore);

        var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, budget: 10);

        Assert.True(result.Connected);
        // Only discoverable if fromB's issues are checked against fromA (not just the
        // seeds) — that's the "full accumulated set" behavior this test exists to prove.
        Assert.Contains(graphStore.Connections, c => c.ComicIssueId == 999);
    }

    [Fact]
    public async Task BudgetExhausted_StopsAndReportsNotConnected()
    {
        const int fromA = 10;
        const int fromB = 20;
        var characters = new Dictionary<int, ComicVineCharacter>
        {
            [SeedA] = Character(SeedA, "A", friends: [fromA], issues: [111]),
            // Same 3-hop chain as the full-accumulated-set test: reaching it needs both
            // fromA and fromB fetched; budget only allows the first.
            [fromA] = Character(fromA, "FromA", issues: [111, 999]),
            [fromB] = Character(fromB, "FromB", issues: [999, 222]),
            [SeedB] = Character(SeedB, "B", friends: [fromB], issues: [222]),
        };
        var graphStore = new FakeGraphStore();
        var characterSource = new FakeComicVineCharacterSource(characters);
        var crawler = new ConnectionCrawler(characterSource, graphStore);

        var result = await crawler.PopulateConnectionsAsync(SeedA, SeedB, budget: 1);

        Assert.False(result.Connected);
        Assert.Equal(1, result.CharactersFetched);
    }

    [Fact]
    public async Task SmallerFrontierIsExpandedFirst()
    {
        var characters = new Dictionary<int, ComicVineCharacter>
        {
            [SeedA] = Character(SeedA, "A", friends: [10], issues: [111]),
            [SeedB] = Character(SeedB, "B", friends: [20, 21, 22], issues: [222]),
            [10] = Character(10, "FromA", issues: [333]),
            [20] = Character(20, "FromB1", issues: [444]),
            [21] = Character(21, "FromB2", issues: [555]),
            [22] = Character(22, "FromB3", issues: [666]),
        };
        var graphStore = new FakeGraphStore();
        var characterSource = new FakeComicVineCharacterSource(characters);
        var crawler = new ConnectionCrawler(characterSource, graphStore);

        // A's frontier (1 friend) is smaller than B's (3 friends) — the one expansion the
        // budget allows should come from A's side.
        await crawler.PopulateConnectionsAsync(SeedA, SeedB, budget: 1);

        Assert.Equal([SeedA, SeedB, 10], characterSource.FetchedIds);
    }

    [Fact]
    public async Task ConnectsToACharacterFromAnEarlierUnrelatedCrawl_NotJustThisRunsDiscoveries()
    {
        const int fromA = 10;
        const int earlierRunCharacter = 999;
        var characters = new Dictionary<int, ComicVineCharacter>
        {
            [SeedA] = Character(SeedA, "A", friends: [fromA], issues: [111]),
            [SeedB] = Character(SeedB, "B", issues: [222]),
            // Shares an issue with earlierRunCharacter, who this crawl never fetches — only
            // reachable via the graph-wide overlap check (ADR-0012), not an in-run dictionary.
            [fromA] = Character(fromA, "FromA", issues: [555]),
        };
        var graphStore = new FakeGraphStore();
        // Simulates a character already persisted by some earlier, unrelated crawl.
        await graphStore.UpsertCharacterAsync(new(earlierRunCharacter, "EarlierRunCharacter"));
        await graphStore.UpsertCharacterIssueCreditsAsync(earlierRunCharacter, [555]);
        var characterSource = new FakeComicVineCharacterSource(characters);
        var crawler = new ConnectionCrawler(characterSource, graphStore);

        await crawler.PopulateConnectionsAsync(SeedA, SeedB, budget: 1);

        Assert.DoesNotContain(earlierRunCharacter, characterSource.FetchedIds);
        Assert.Contains(graphStore.Connections, c =>
            c.ComicIssueId == 555 &&
            (c.SourceCharacterComicVineId == fromA || c.TargetCharacterComicVineId == fromA) &&
            (c.SourceCharacterComicVineId == earlierRunCharacter || c.TargetCharacterComicVineId == earlierRunCharacter));
    }

    [Fact]
    public async Task NeverFetchesTheSameCharacterTwiceEvenIfReachableFromBothSides()
    {
        const int mutualFriend = 30;
        var characters = new Dictionary<int, ComicVineCharacter>
        {
            [SeedA] = Character(SeedA, "A", friends: [mutualFriend], issues: [111]),
            [SeedB] = Character(SeedB, "B", friends: [mutualFriend], issues: [222]),
            [mutualFriend] = Character(mutualFriend, "Mutual", issues: [333]),
        };
        var graphStore = new FakeGraphStore();
        var characterSource = new FakeComicVineCharacterSource(characters);
        var crawler = new ConnectionCrawler(characterSource, graphStore);

        await crawler.PopulateConnectionsAsync(SeedA, SeedB, budget: 10);

        Assert.Single(characterSource.FetchedIds, id => id == mutualFriend);
    }
}
