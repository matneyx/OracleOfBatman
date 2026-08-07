using OracleOfBatman.Domain;
using Path = OracleOfBatman.Domain.Path;

namespace OracleOfBatman.Graph.Tests.Fakes;

/// <summary>
///   In-memory IGraphStore so ConnectionCrawler's decision logic is testable without
///   Docker/Neo4j. Path existence is BFS over an undirected adjacency built from Connections,
///   matching Neo4jGraphWriter.PathExistsAsync's undirected Cypher pattern.
/// </summary>
public sealed class FakeGraphStore : IGraphStore
{
  private readonly Dictionary<int, HashSet<int>> _adjacency = [];
  private readonly Dictionary<int, HashSet<int>> _characterIssueCredits = []; // character id -> their own issue_credits
  private readonly Dictionary<int, Character> _characters = [];
  private readonly List<Connection> _connections = [];

  private readonly Dictionary<int, HashSet<int>>
    _issueCharacterCredits = []; // issue id -> materialized character_credits

  private readonly Dictionary<int, Issue> _issues = [];

  public IReadOnlyList<Character> Characters => [.. _characters.Values];

  public IReadOnlyList<Connection> Connections => _connections;

  public IReadOnlyDictionary<int, IReadOnlyCollection<int>> IssueCharacterCredits
    => _issueCharacterCredits.ToDictionary<KeyValuePair<int, HashSet<int>>, int, IReadOnlyCollection<int>>(x => x.Key,
      x => [.. x.Value]);

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
        return await ReconstructPathAsync(characterBComicVineId, parent);
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

  public Task UpsertCharacterAsync(Character character)
  {
    _characters[character.ComicVineId] = character;
    return Task.CompletedTask;
  }

  public Task<Character?> GetCharacterAsync(int comicVineId) =>
    Task.FromResult(_characters.GetValueOrDefault(comicVineId));

  public Task<Issue?> GetIssueAsync(int comicVineId) => _issues.TryGetValue(comicVineId, out var issue)
    ? Task.FromResult(issue)
    : Task.FromResult<Issue?>(null);

  public Task UpsertIssueAsync(Issue issue)
  {
    _issues[issue.ComicVineId] = issue;
    return Task.CompletedTask;
  }

  public Task UpsertCharacterIssueCreditsAsync(int comicVineCharacterId, IReadOnlyList<int> issueCreditIds)
  {
    _characterIssueCredits[comicVineCharacterId] = [.. issueCreditIds];
    return Task.CompletedTask;
  }

  public Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> FindOverlappingIssuesAsync(int comicVineCharacterId,
    IReadOnlyList<int> issueCreditIds)
  {
    var result = new Dictionary<int, IReadOnlyList<int>>();

    foreach (var (otherCharacterId, otherIssues) in _characterIssueCredits)
    {
      if (otherCharacterId == comicVineCharacterId)
      {
        continue;
      }

      var sharedIssueIds = otherIssues.Intersect(issueCreditIds).ToHashSet();

      if (sharedIssueIds.Count > 0)
      {
        result[otherCharacterId] = [.. sharedIssueIds];

        foreach (var issueId in sharedIssueIds)
        {
          _issues.TryAdd(issueId, new Issue(issueId, null));

          var credits = _issueCharacterCredits.GetValueOrDefault(issueId) ?? [];
          credits.Add(comicVineCharacterId);
          credits.Add(otherCharacterId);
          _issueCharacterCredits[issueId] = credits;
        }
      }
    }

    return Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<int>>>(result);
  }

  public Task UpsertConnectionAsync(Connection connection)
  {
    _connections.Add(connection);

    _adjacency.TryAdd(connection.SourceCharacterComicVineId, []);
    _adjacency.TryAdd(connection.TargetCharacterComicVineId, []);
    _adjacency[connection.SourceCharacterComicVineId].Add(connection.TargetCharacterComicVineId);
    _adjacency[connection.TargetCharacterComicVineId].Add(connection.SourceCharacterComicVineId);

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

  private Path ReconstructPath(int startId, int endId, Dictionary<int, int> parent)
  {
    var idPath = new List<int> { endId };
    while (parent.TryGetValue(idPath[^1], out var previous))
    {
      idPath.Add(previous);
    }

    idPath.Reverse();

    var characters = idPath.Select(id => _characters[id]).ToList();
    var hops = new List<Hop>();
    for (var i = 0; i < idPath.Count - 1; i++)
    {
      var connection = _connections.First(c =>
        (c.SourceCharacterComicVineId == idPath[i] && c.TargetCharacterComicVineId == idPath[i + 1]) ||
        (c.SourceCharacterComicVineId == idPath[i + 1] && c.TargetCharacterComicVineId == idPath[i]));
      hops.Add(new Hop(characters[i], characters[i + 1],
        new Issue(connection.ComicIssueId.Value, connection.ComicIssueName,
          SiteDetailUrl: connection.ComicIssueSiteDetailUrl)));
    }

    return new Path(characters, hops);
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
