using OracleOfBatman.Domain;

namespace OracleOfBatman.Graph.ComicVine;

public static class ComicVineMapper
{
    public static Character ToDomain(this ComicVineCharacter source) =>
        new(source.Id, source.Name, source.Image?.IconUrl, source.SiteDetailUrl);

    public static Character ToDomain(this ComicVineCharacterRef source) => new(source.Id, source.Name);
}
