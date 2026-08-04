using OracleOfBatman.Graph.ComicVine;

namespace OracleOfBatman.Graph.Tests.Fakes;

/// <summary>In-memory IComicVineCharacterSource backed by a hand-built character graph, so
/// ConnectionCrawler tests don't depend on real HTTP or the large real sample files.</summary>
public sealed class FakeComicVineCharacterSource(Dictionary<int, ComicVineCharacter> characters) : IComicVineCharacterSource
{
    private readonly List<int> _fetchedIds = [];

    public IReadOnlyList<int> FetchedIds => _fetchedIds;

    public Task<ComicVineCharacter> GetCharacterAsync(int comicVineId)
    {
        _fetchedIds.Add(comicVineId);

        if (!characters.TryGetValue(comicVineId, out var character))
        {
            throw new KeyNotFoundException($"No fake character registered for Comic Vine id {comicVineId}");
        }

        return Task.FromResult(character);
    }
}
