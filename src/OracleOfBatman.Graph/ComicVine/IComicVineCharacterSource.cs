namespace OracleOfBatman.Graph.ComicVine;

/// <summary>
///   Fetches a single character by Comic Vine id. ComicVineApiClient is the real,
///   HTTP-backed implementation; tests use an in-memory fake.
/// </summary>
public interface IComicVineCharacterSource
{
  Task<ComicVineCharacter> GetCharacterAsync(int comicVineId);
}
