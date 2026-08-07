using OracleOfBatman.Graph.ComicVine;

namespace OracleOfBatman.Web.Tests.Fakes;

/// <summary>
///   Minimal IComicVineCharacterSearchSource so Home.razor's Comic Vine search section
///   renders in tests — registering this is what makes `_comicVineSearchSource` non-null.
/// </summary>
public sealed class StubComicVineCharacterSearchSource : IComicVineCharacterSearchSource
{
  public Task<IReadOnlyList<ComicVineSearchCharacterResult>> SearchCharactersAsync(string query) =>
    Task.FromResult<IReadOnlyList<ComicVineSearchCharacterResult>>([]);
}
