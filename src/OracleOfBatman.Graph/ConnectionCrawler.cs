using OracleOfBatman.Domain;
using OracleOfBatman.Graph.ComicVine;

namespace OracleOfBatman.Graph;

public sealed record CrawlResult(bool Connected, int CharactersFetched);

/// <summary>
///   Implements ADR-0010's bidirectional friend/enemy-BFS crawl: free existing-path pre-check,
///   then direct issue/friend-enemy overlap checks, then budget-bounded bidirectional BFS
///   (smaller-frontier-first). Overlap checks go through the graph itself (ADR-0012), covering
///   every character ever persisted — not just ones discovered in this run.
/// </summary>
public sealed class ConnectionCrawler(
  IComicVineCharacterSource characterSource,
  IGraphStore graphStore)
{
  private readonly Queue<int> _frontierA = [];
  private readonly Queue<int> _frontierB = [];
  private readonly HashSet<int> _visited = [];

  public async Task<CrawlResult> PopulateConnectionsAsync(int seedAComicVineId, int seedBComicVineId, int budget)
  {
    if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId))
    {
      return new CrawlResult(true, 0);
    }

    // The two seed fetches aren't counted against the expansion budget — the budget is
    // for new characters discovered beyond the seeds (ADR-0010).
    var seedA = await IngestCharacterAsync(seedAComicVineId);
    var seedB = await IngestCharacterAsync(seedBComicVineId);

    if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId))
    {
      return new CrawlResult(true, 0);
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
        return new CrawlResult(false, fetched);
      }

      if (_visited.Contains(candidateId))
      {
        continue;
      }

      await IngestCharacterAsync(candidateId);
      fetched++;

      if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId))
      {
        return new CrawlResult(true, fetched);
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
        return new CrawlResult(true, fetched);
      }
    }

    return new CrawlResult(false, fetched);
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
    foreach (var id in character.CharacterFriends.Select(f => f.Id)
      .Concat(character.CharacterEnemies.Select(e => e.Id)))
    {
      if (!frontier.Contains(id))
      {
        frontier.Enqueue(id);
      }
    }
  }

  /// <summary>
  ///   Ensures a character is fully persisted (Character node + issue_credits) and
  ///   checked for overlaps against the whole graph (ADR-0012). Public because it's also
  ///   useful standalone — e.g. seeding a single character picked from a Comic Vine search
  ///   that isn't in our graph yet at all.
  /// </summary>
  public async Task<ComicVineCharacter> IngestCharacterAsync(int comicVineId)
  {
    var character = await characterSource.GetCharacterAsync(comicVineId);
    _visited.Add(comicVineId);

    var isNewCharacter = await graphStore.GetCharacterAsync(comicVineId) is null;
    await graphStore.UpsertCharacterAsync(character.ToDomain());
    if (isNewCharacter)
    {
      CharacterAdded?.Invoke(character.ToDomain());
    }

    var issueCreditIds = character.IssueCredits.Select(i => i.Id).ToList();
    await graphStore.UpsertCharacterIssueCreditsAsync(comicVineId, issueCreditIds);

    var overlaps = await graphStore.FindOverlappingIssuesAsync(comicVineId, issueCreditIds);
    if(overlaps.Any() && overlaps.Values.Any())
    {
        var allUniqueIssueIds = overlaps.Values.SelectMany(i => i).ToHashSet();

        foreach (var issueId in allUniqueIssueIds)
        {
          var issueRef = character.IssueCredits.First(i => i.Id == issueId);
          IssueConnectionConfirmed?.Invoke(new Issue(issueId, issueRef.Name, SiteDetailUrl: issueRef.SiteDetailUrl));
        }
    }

    return character;
  }

  private enum Side
  {
    A,
    B
  }

  public event Action<Character>? CharacterAdded;
  public event Action<Issue>? IssueConnectionConfirmed;
}
