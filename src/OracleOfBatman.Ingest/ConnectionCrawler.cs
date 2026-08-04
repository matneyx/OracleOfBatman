using OracleOfBatman.Domain;
using OracleOfBatman.Graph;
using OracleOfBatman.Ingest.ComicVine;

namespace OracleOfBatman.Ingest;

public sealed record CrawlResult(bool Connected, int CharactersFetched);

/// <summary>
/// Implements ADR-0010's bidirectional friend/enemy-BFS crawl: free existing-path pre-check,
/// then direct issue/friend-enemy overlap checks, then budget-bounded bidirectional BFS
/// (smaller-frontier-first). Overlap checks go through the graph itself (ADR-0012), covering
/// every character ever persisted — not just ones discovered in this run.
/// </summary>
public sealed class ConnectionCrawler(IComicVineCharacterSource characterSource, IGraphStore graphStore)
{
    private readonly HashSet<int> _visited = [];
    private readonly Queue<int> _frontierA = [];
    private readonly Queue<int> _frontierB = [];

    public async Task<CrawlResult> PopulateConnectionsAsync(int seedAComicVineId, int seedBComicVineId, int budget)
    {
        if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId))
        {
            return new CrawlResult(Connected: true, CharactersFetched: 0);
        }

        // The two seed fetches aren't counted against the expansion budget — the budget is
        // for new characters discovered beyond the seeds (ADR-0010).
        var seedA = await FetchAndRecordAsync(seedAComicVineId);
        var seedB = await FetchAndRecordAsync(seedBComicVineId);

        if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId))
        {
            return new CrawlResult(Connected: true, CharactersFetched: 0);
        }

        EnqueueFrontier(_frontierA, seedA);
        EnqueueFrontier(_frontierB, seedB);

        var fetched = 0;

        // Direct friend/enemy overlap between the seeds: process known-common candidates
        // first, since they're the cheapest, highest-confidence bridge candidates.
        var common = _frontierA.Intersect(_frontierB).ToList();
        foreach (var candidateId in common)
        {
            if (fetched >= budget)
            {
                return new CrawlResult(Connected: false, CharactersFetched: fetched);
            }

            if (_visited.Contains(candidateId))
            {
                continue;
            }

            await FetchAndRecordAsync(candidateId);
            fetched++;

            if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId))
            {
                return new CrawlResult(Connected: true, CharactersFetched: fetched);
            }
        }

        // Bidirectional BFS: expand whichever frontier is smaller, one new character at a
        // time.
        while (fetched < budget && (_frontierA.Count > 0 || _frontierB.Count > 0))
        {
            var side = ChooseSideToExpand();
            var candidateId = DequeueNextUnvisited(side);
            if (candidateId is null)
            {
                continue;
            }

            var newCharacter = await FetchAndRecordAsync(candidateId.Value);
            fetched++;
            EnqueueFrontier(side == Side.A ? _frontierA : _frontierB, newCharacter);

            if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId))
            {
                return new CrawlResult(Connected: true, CharactersFetched: fetched);
            }
        }

        return new CrawlResult(Connected: false, CharactersFetched: fetched);
    }

    private enum Side
    {
        A,
        B,
    }

    private Side ChooseSideToExpand()
    {
        if (_frontierA.Count == 0)
        {
            return Side.B;
        }

        if (_frontierB.Count == 0)
        {
            return Side.A;
        }

        return _frontierA.Count <= _frontierB.Count ? Side.A : Side.B;
    }

    private int? DequeueNextUnvisited(Side side)
    {
        var frontier = side == Side.A ? _frontierA : _frontierB;
        while (frontier.Count > 0)
        {
            var candidateId = frontier.Dequeue();
            if (!_visited.Contains(candidateId))
            {
                return candidateId;
            }
        }

        return null;
    }

    private static void EnqueueFrontier(Queue<int> frontier, ComicVineCharacter character)
    {
        foreach (var id in character.CharacterFriends.Select(f => f.Id).Concat(character.CharacterEnemies.Select(e => e.Id)))
        {
            if (!frontier.Contains(id))
            {
                frontier.Enqueue(id);
            }
        }
    }

    private async Task<ComicVineCharacter> FetchAndRecordAsync(int comicVineId)
    {
        var character = await characterSource.GetCharacterAsync(comicVineId);
        _visited.Add(comicVineId);

        await graphStore.UpsertCharacterAsync(character.ToDomain());

        var issueCreditIds = character.IssueCredits.Select(i => i.Id).ToList();
        await graphStore.UpsertCharacterIssueCreditsAsync(comicVineId, issueCreditIds);

        // Checks the WHOLE persisted graph, not just this run's discoveries (ADR-0012) — a
        // character found by some earlier, unrelated crawl still gets connected here if their
        // issue lists overlap.
        var overlaps = await graphStore.FindOverlappingIssuesAsync(comicVineId, issueCreditIds);
        foreach (var (otherId, sharedIssueIds) in overlaps)
        {
            foreach (var issueId in sharedIssueIds)
            {
                var connection = new Connection(
                    comicVineId,
                    otherId,
                    issueId,
                    ComicIssuePublishedAt: null,
                    InteractionTier.SameIssue,
                    Confidence.Unverified);
                await graphStore.UpsertConnectionAsync(connection);
            }
        }

        return character;
    }
}
