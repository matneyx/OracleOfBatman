using OracleOfBatman.Domain;
using OracleOfBatman.Ingest.ComicVine;

namespace OracleOfBatman.Ingest;

public sealed record CrawlResult(bool Connected, int CharactersFetched);

/// <summary>
/// Implements ADR-0010's bidirectional friend/enemy-BFS crawl: free existing-path pre-check,
/// then direct issue/friend-enemy overlap checks, then budget-bounded bidirectional BFS
/// (smaller-frontier-first) that checks every newly-fetched character against the full
/// accumulated set, not just the two seeds.
/// </summary>
public sealed class ConnectionCrawler(IComicVineCharacterSource characterSource, IGraphStore graphStore)
{
    private readonly Dictionary<int, ComicVineCharacter> _known = [];
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

            if (_known.ContainsKey(candidateId))
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
        // time, checking each against everyone discovered so far (not just the seeds).
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
            if (!_known.ContainsKey(candidateId))
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
        _known[comicVineId] = character;

        await graphStore.UpsertCharacterAsync(character.ToDomain());
        await CheckOverlapsAgainstKnownAsync(character);

        return character;
    }

    private async Task CheckOverlapsAgainstKnownAsync(ComicVineCharacter newCharacter)
    {
        foreach (var (existingId, existingCharacter) in _known)
        {
            if (existingId == newCharacter.Id)
            {
                continue;
            }

            var sharedIssueIds = newCharacter.IssueCredits.Select(i => i.Id)
                .Intersect(existingCharacter.IssueCredits.Select(i => i.Id));

            foreach (var issueId in sharedIssueIds)
            {
                var connection = new Connection(
                    newCharacter.Id,
                    existingId,
                    issueId,
                    ComicIssuePublishedAt: null,
                    InteractionTier.SharedScene,
                    Confidence.Unverified);
                await graphStore.UpsertConnectionAsync(connection);
            }
        }
    }
}
