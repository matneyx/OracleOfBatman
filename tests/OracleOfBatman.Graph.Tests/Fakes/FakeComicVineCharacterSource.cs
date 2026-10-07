using OracleOfBatman.Graph.ComicVine;

namespace OracleOfBatman.Graph.Tests.Fakes;

/// <summary>
///   In-memory IComicVineCharacterSource backed by a hand-built character graph, so
///   ConnectionCrawler tests don't depend on real HTTP or the large real sample files.
/// </summary>
public sealed class FakeComicVineCharacterSource(Dictionary<int, ComicVineCharacter> characters)
  : IComicVineCharacterSource
{
  private readonly List<int> _fetchedIds = [];

  public IReadOnlyList<int> FetchedIds => _fetchedIds;

  /// <summary>
  ///   Runs after each fetch is recorded — lets a test act at an exact point mid-crawl
  ///   (e.g. cancel once a given character has been fetched). Deliberately ignores the
  ///   token itself, so tests prove the crawler checks it rather than leaning on a source.
  /// </summary>
  public Action<int>? OnFetched { get; set; }

  public Task<ComicVineCharacter> GetCharacterAsync(int comicVineId, CancellationToken token)
  {
    _fetchedIds.Add(comicVineId);
    OnFetched?.Invoke(comicVineId);

    if (!characters.TryGetValue(comicVineId, out var character))
    {
      throw new KeyNotFoundException($"No fake character registered for Comic Vine id {comicVineId}");
    }

    return Task.FromResult(character);
  }
}
