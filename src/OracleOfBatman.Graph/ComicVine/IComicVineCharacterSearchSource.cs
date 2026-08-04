namespace OracleOfBatman.Graph.ComicVine;

/// <summary>Searches Comic Vine's own database by name — for when a character isn't in our
/// Neo4j graph yet at all. Full-text across bio/description too, not a pure name match.</summary>
public interface IComicVineCharacterSearchSource
{
    Task<IReadOnlyList<ComicVineSearchCharacterResult>> SearchCharactersAsync(string query);
}
