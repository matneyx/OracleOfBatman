using OracleOfBatman.Domain;

namespace OracleOfBatman.Graph.ComicVine;

public static class ComicVineMapper
{
  public static Character ToDomain(this ComicVineCharacter source) =>
    new(source.Id, source.Name, imageUrl: source.Image?.IconUrl, siteDetailUrl: source.SiteDetailUrl,
      friendIds: [.. source.CharacterFriends.Select(f => f.Id)],
      enemyIds: [.. source.CharacterEnemies.Select(e => e.Id)]);

  public static Character ToDomain(this ComicVineCharacterRef source) => new(source.Id, source.Name);
}
