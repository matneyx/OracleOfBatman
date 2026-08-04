using OracleOfBatman.Domain;

namespace OracleOfBatman.Ingest.Tests.Fakes;

/// <summary>In-memory IGraphStore so ConnectionCrawler's decision logic is testable without
/// Docker/Neo4j. Path existence is BFS over an undirected adjacency built from Connections,
/// matching Neo4jGraphWriter.PathExistsAsync's undirected Cypher pattern.</summary>
public sealed class FakeGraphStore : IGraphStore
{
    private readonly Dictionary<int, Character> _characters = [];
    private readonly List<Connection> _connections = [];
    private readonly Dictionary<int, HashSet<int>> _adjacency = [];

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

    public Task UpsertCharacterAsync(Character character)
    {
        _characters[character.ComicVineId] = character;
        return Task.CompletedTask;
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
}
