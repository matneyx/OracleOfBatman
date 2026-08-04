using OracleOfBatman.Domain;
using OracleOfBatman.Graph.ComicVine;

namespace OracleOfBatman.Graph;

public sealed record CrawlResult(bool Connected, int CharactersFetched);

/// <summary>
/// Implements ADR-0010's bidirectional friend/enemy-BFS crawl: free existing-path pre-check,
/// then direct issue/friend-enemy overlap checks, then budget-bounded bidirectional BFS
/// (smaller-frontier-first). Overlap checks go through the graph itself (ADR-0012), covering
/// every character ever persisted — not just ones discovered in this run.
/// </summary>
public sealed class ConnectionCrawler(IComicVineCharacterSource characterSource, IComicVineIssueSource issueSource, IGraphStore graphStore)
{
    private readonly HashSet<int> _visited = [];
    private readonly Queue<int> _frontierA = [];
    private readonly Queue<int> _frontierB = [];
    private readonly Dictionary<int, ComicVineIssue> _issueCache = [];
    private readonly HashSet<int> _issueLookupFailures = [];

    public async Task<CrawlResult> PopulateConnectionsAsync(int seedAComicVineId, int seedBComicVineId, int budget)
    {
        if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId))
        {
            return new CrawlResult(Connected: true, CharactersFetched: 0);
        }

        // The two seed fetches aren't counted against the expansion budget — the budget is
        // for new characters discovered beyond the seeds (ADR-0010).
        var seedA = await IngestCharacterAsync(seedAComicVineId);
        var seedB = await IngestCharacterAsync(seedBComicVineId);

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

            await IngestCharacterAsync(candidateId);
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

            var newCharacter = await IngestCharacterAsync(candidateId.Value);
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

    /// <summary>Ensures a character is fully persisted (Character node + issue_credits) and
    /// checked for overlaps against the whole graph (ADR-0012). Public because it's also
    /// useful standalone — e.g. seeding a single character picked from a Comic Vine search
    /// that isn't in our graph yet at all.</summary>
    public async Task<ComicVineCharacter> IngestCharacterAsync(int comicVineId)
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
                // The matched issue's name/link usually come free from this character's own
                // issue_credits — no extra Comic Vine request. Collected editions (TPBs,
                // omnibuses) often have a blank or generic "TPB" issue name, though; the only
                // identifying info Comic Vine gives for those is the Volume's (series) name, so
                // that's worth the one extra request per such issue.
                var issueRef = character.IssueCredits.First(i => i.Id == issueId);
                var issueName = await ResolveIssueNameAsync(issueId, issueRef.Name);
                var connection = new Connection(
                    comicVineId,
                    otherId,
                    issueId,
                    ComicIssuePublishedAt: null,
                    InteractionTier.SameIssue,
                    Confidence.Unverified,
                    issueName,
                    issueRef.SiteDetailUrl);
                await graphStore.UpsertConnectionAsync(connection);
            }
        }

        return character;
    }

    /// <summary>Enriches the issue name with its Volume (series) name when the issue's own name
    /// is blank or the generic "TPB" — the common case for collected editions. Blank names are
    /// replaced by the Volume name alone; "TPB" is kept alongside it ("{Volume}: TPB") since it
    /// still carries some signal. Caches the fetched issue per crawl run since the same issue
    /// often bridges several overlapping characters.</summary>
    private async Task<string?> ResolveIssueNameAsync(int issueId, string? issueName)
    {
        if (!string.IsNullOrWhiteSpace(issueName) && !string.Equals(issueName, "TPB", StringComparison.OrdinalIgnoreCase))
        {
            return issueName;
        }

        if (_issueLookupFailures.Contains(issueId))
        {
            return issueName;
        }

        if (!_issueCache.TryGetValue(issueId, out var issue))
        {
            try
            {
                issue = await issueSource.GetIssueAsync(issueId);
            }
            catch (HttpRequestException)
            {
                // Comic Vine being unreachable or rate-limited (a real risk here — a prolific
                // character can trigger dozens of these lookups in one ingest) must not abort
                // the whole crawl. Fall back to the original name and don't retry this issue
                // again this run.
                _issueLookupFailures.Add(issueId);
                return issueName;
            }

            _issueCache[issueId] = issue;
        }

        if (issue.Volume?.Name is not { } volumeName)
        {
            return issueName;
        }

        // "TPB" still carries some signal (it's not nothing), so keep it alongside the
        // Volume rather than discarding it — but a genuinely blank issue name has nothing
        // worth keeping, so the Volume alone is the whole answer there.
        return string.IsNullOrWhiteSpace(issueName) ? volumeName : $"{volumeName}: {issueName}";
    }
}
