using OracleOfBatman.Domain;
using Path = OracleOfBatman.Domain.Path;

namespace OracleOfBatman.Graph.Tests.Fakes;

/// <summary>
///   In-memory IGraphStore so ConnectionCrawler's decision logic is testable without
///   Docker/Neo4j. Path existence is BFS over adjacency built from CREDITED_IN edges
///   (ADR-0016), matching Neo4jGraphWriter.PathExistsAsync's shortestPath() traversal.
/// </summary>
public sealed class FakeGraphStore : IGraphStore
{
  private readonly Dictionary<int, HashSet<int>> _characterIssueCredits = []; // character id -> credited issue ids
  private readonly Dictionary<int, Character> _characters = [];

  private readonly Dictionary<int, HashSet<int>>
    _issueCharacterCredits = []; // issue id -> credited character ids

  private readonly Dictionary<int, Issue> _issues = [];

  public IReadOnlyList<Character> Characters => [.. _characters.Values];

  public Task<bool> PathExistsAsync(int characterAComicVineId, int characterBComicVineId)
  {
    if (!_characters.ContainsKey(characterAComicVineId) || !_characters.ContainsKey(characterBComicVineId))
    {
      return Task.FromResult(false);
    }

    var visited = new HashSet<int> { characterAComicVineId };
    var queue = new Queue<int>();
    queue.Enqueue(characterAComicVineId);

    while (queue.Count > 0)
    {
      var current = queue.Dequeue();
      if (current == characterBComicVineId)
      {
        return Task.FromResult(true);
      }

      foreach (var neighbor in GetNeighborCharacters(current).Keys)
      {
        if (visited.Add(neighbor))
        {
          queue.Enqueue(neighbor);
        }
      }
    }

    return Task.FromResult(characterAComicVineId == characterBComicVineId);
  }

  public async Task<Path?> FindShortestPathAsync(int characterAComicVineId, int characterBComicVineId, int maxDepth)
  {
    var visited = new HashSet<int> { characterAComicVineId };
    var parent = new Dictionary<int, (int PreviousCharacterId, int IssueId)>();
    var queue = new Queue<(int Id, int Depth)>();
    queue.Enqueue((characterAComicVineId, 0));
    visited.Add(characterAComicVineId);

    while (queue.Count > 0)
    {
      var (current, depth) = queue.Dequeue();
      if (current == characterBComicVineId)
      {
        var path = await ReconstructPathAsync(characterBComicVineId, parent);
        IncrementUsageCounts(path);
        return path;
      }

      if (depth >= maxDepth) continue;

      foreach (var (neighborId, issueId) in GetNeighborCharacters(current))
      {
        if (visited.Add(neighborId))
        {
          parent[neighborId] = (current, issueId);
          queue.Enqueue((neighborId, depth + 1));
        }
      }
    }

    return null;
  }

  public async Task RecordSeedUseAsync(int characterAComicVineId, int characterBComicVineId)
  {
      _characters[characterAComicVineId].SeedUseCount++;
      _characters[characterBComicVineId].SeedUseCount++;
  }

  public Task UpsertCharacterAsync(Character character)
  {
    _characters[character.ComicVineId] = character;
    return Task.CompletedTask;
  }

  public Task<Character?> GetCharacterAsync(int comicVineId) =>
    Task.FromResult(_characters.GetValueOrDefault(comicVineId));

  public Task UpsertCreditedInAsync(int comicVineCharacterId, IReadOnlyList<Issue> issueCredits)
  {
    try
    {
      if(!_characterIssueCredits.ContainsKey(comicVineCharacterId))
      {
        _characterIssueCredits[comicVineCharacterId] = [];
      }

      foreach (var issue in issueCredits)
      {
        // Add issue to _characterIssueCredits
        _characterIssueCredits[comicVineCharacterId].Add(issue.ComicVineId);

        // Ensure that _issueCharacterCredits has the issue
        if(!_issueCharacterCredits.ContainsKey(issue.ComicVineId))
        {
          _issueCharacterCredits[issue.ComicVineId] = [];
        }

        // Add character to _issueCharacterCredits
        _issueCharacterCredits[issue.ComicVineId].Add(comicVineCharacterId);

        // Attempt to add issue to _issues; TryAdd so we don't overwrite existing issues
        _issues.TryAdd(issue.ComicVineId, issue);
      }

      return Task.CompletedTask;
    }
    catch (Exception exception)
    {
      return Task.FromException(exception);
    }
  }

  public Task<Issue?> GetIssueAsync(int comicVineId) => _issues.TryGetValue(comicVineId, out var issue)
    ? Task.FromResult(issue)
    : Task.FromResult<Issue?>(null);

  public Task UpsertIssueAsync(Issue issue)
  {
    _issues[issue.ComicVineId] = issue;
    return Task.CompletedTask;
  }

  public Task<IReadOnlyList<Character>> SearchCharactersAsync(string query, int limit = 20)
  {
    var results = _characters.Values
      .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
      .OrderBy(c => c.Name, StringComparer.Ordinal)
      .Take(limit)
      .ToList();

    return Task.FromResult<IReadOnlyList<Character>>(results);
  }

  public Task<Character?> GetLeastRecentlyIngestedCharacterAsync(IReadOnlyCollection<int> excludedIds)
  {
    var candidate = _characters.Values
      .Where(c => c.IngestionDateTime is not null && !excludedIds.Contains(c.ComicVineId))
      .OrderBy(c => c.IngestionDateTime)
      .FirstOrDefault();

    return Task.FromResult(candidate);
  }

  private void IncrementUsageCounts(Path path)
  {
    foreach (var bridge in path.Characters.Skip(1).SkipLast(1))
    {
      _characters[bridge.ComicVineId] = bridge with { BridgeUseCount = bridge.BridgeUseCount + 1 };
    }

    foreach (var hop in path.Hops)
    {
      _issues[hop.Issue.ComicVineId] = hop.Issue with { PathUseCount = hop.Issue.PathUseCount + 1 };
    }
  }

  private async Task<Path> ReconstructPathAsync(int endingCharacterId,
    Dictionary<int, (int PreviousCharacterId, int IssueId)> parent)
  {
    var idPath = new List<int> { endingCharacterId };

    var characters = new Dictionary<int, Character>();
    var endingCharacter = await GetCharacterAsync(endingCharacterId);
    characters.Add(endingCharacterId, endingCharacter);

    var hops = new List<Hop>();

    while (parent.TryGetValue(idPath[^1], out var link))
    {
      var currentId = idPath[^1];
      idPath.Add(link.PreviousCharacterId);

      var newCharacter = await GetCharacterAsync(link.PreviousCharacterId);
      characters[link.PreviousCharacterId] = newCharacter!;

      var issue = await GetIssueAsync(link.IssueId);

      hops.Add(new Hop(characters[link.PreviousCharacterId], characters[currentId], issue!));
    }

    idPath.Reverse();
    hops.Reverse();

    return new Path([.. idPath.Select(id => characters[id])], hops);
  }

  private IReadOnlyDictionary<int, int> GetNeighborCharacters(int comicVineId)
  {
    var result = new Dictionary<int, int>();

    foreach (var issueId in _characterIssueCredits.GetValueOrDefault(comicVineId, []))
    {
      foreach (var neighborId in _issueCharacterCredits.GetValueOrDefault(issueId, []))
      {
        if (neighborId != comicVineId)
        {
          result.TryAdd(neighborId, issueId);
        }
      }
    }

    return result;
  }
}
