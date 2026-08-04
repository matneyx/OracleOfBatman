using OracleOfBatman.Domain;

namespace OracleOfBatman.Ingest.ComicVine;

public static class ComicVineMapper
{
    public static Character ToDomain(this ComicVineCharacter source) => new(source.Id, source.Name);

    public static Character ToDomain(this ComicVineCharacterRef source) => new(source.Id, source.Name);
}
