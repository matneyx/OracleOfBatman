using OracleOfBatman.Domain;

namespace OracleOfBatman.Graph.Tests.Fakes;

/// <summary>In-memory IGraphStore so ConnectionCrawler's decision logic is testable without
/// Docker/Neo4j. Path existence is BFS over an undirected adjacency built from Connections,
/// matching Neo4jGraphWriter.PathExistsAsync's undirected Cypher pattern.</summary>
public sealed class FakeGraphStore : IGraphStore
{
    private readonly Dictionary<int, Character> _characters = [];
    private readonly List<Connection> _connections = [];
    private readonly Dictionary<int, HashSet<int>> _adjacency = [];
    private readonly Dictionary<int, IReadOnlyList<int>> _issueCredits = [];

    public IReadOnlyList<Character> Characters => _characters.Values.ToList();

    public IReadOnlyList<Connection> Connections => _connections;

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

            foreach (var neighbor in _adjacency.GetValueOrDefault(current, []))
            {
                if (visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return Task.FromResult(characterAComicVineId == characterBComicVineId);
    }

    public Task<Domain.Path?> FindShortestPathAsync(int characterAComicVineId, int characterBComicVineId, int maxDepth)
    {
        if (!_characters.ContainsKey(characterAComicVineId) || !_characters.ContainsKey(characterBComicVineId))
        {
            return Task.FromResult<Domain.Path?>(null);
        }

        var visited = new HashSet<int> { characterAComicVineId };
        var parent = new Dictionary<int, int>();
        var queue = new Queue<(int Id, int Depth)>();
        queue.Enqueue((characterAComicVineId, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (current == characterBComicVineId)
            {
                return Task.FromResult<Domain.Path?>(ReconstructPath(characterAComicVineId, current, parent));
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var neighbor in _adjacency.GetValueOrDefault(current, []))
            {
                if (visited.Add(neighbor))
                {
                    parent[neighbor] = current;
                    queue.Enqueue((neighbor, depth + 1));
                }
            }
        }

        return Task.FromResult<Domain.Path?>(null);
    }

    private Domain.Path ReconstructPath(int startId, int endId, Dictionary<int, int> parent)
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
            hops.Add(new Hop(characters[i], characters[i + 1], connection.ComicIssueId, connection.Tier, connection.Confidence, connection.ComicIssueName, connection.ComicIssueSiteDetailUrl));
        }

        return new Domain.Path(characters, hops);
    }

    public Task UpsertCharacterAsync(Character character)
    {
        _characters[character.ComicVineId] = character;
        return Task.CompletedTask;
    }

    public Task UpsertCharacterIssueCreditsAsync(int comicVineId, IReadOnlyList<int> issueCreditIds)
    {
        _issueCredits[comicVineId] = issueCreditIds;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> FindOverlappingIssuesAsync(int comicVineId, IReadOnlyList<int> issueCreditIds)
    {
        var result = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var (otherId, otherIssues) in _issueCredits)
        {
            if (otherId == comicVineId)
            {
                continue;
            }

            var shared = otherIssues.Intersect(issueCreditIds).ToList();
            if (shared.Count > 0)
            {
                result[otherId] = shared;
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
}
