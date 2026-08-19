namespace OracleOfBatman.Domain;

/// <summary>
///   MVP scope cut: 1:1 with a Comic Vine entry, no Mantle/Portrayal/Universe yet (see docs/MVP.md).
///   ImageUrl/SiteDetailUrl come free on the same character fetch the crawl already makes.
/// </summary>
public sealed record Character
{
  /// <summary>
  ///   MVP scope cut: 1:1 with a Comic Vine entry, no Mantle/Portrayal/Universe yet (see docs/MVP.md).
  ///   ImageUrl/SiteDetailUrl come free on the same character fetch the crawl already makes.
  /// </summary>
  public Character(int comicVineId,
    string name,
    DateTime? ingestionDateTime = null,
    string? imageUrl = null,
    string? siteDetailUrl = null,
    int[]? friendIds = null,
    int[]? enemyIds = null,
    int seedUseCount = 0,
    int bridgeUseCount = 0)
  {
    ComicVineId = comicVineId;
    Name = name;
    IngestionDateTime = ingestionDateTime;
    ImageUrl = imageUrl;
    SiteDetailUrl = siteDetailUrl;
    FriendIds = friendIds ?? [];
    EnemyIds = enemyIds ?? [];
    SeedUseCount = seedUseCount;
    BridgeUseCount = bridgeUseCount;
  }

  public int ComicVineId { get; init; }
  public string Name { get; init; }
  public DateTime? IngestionDateTime { get; init; }
  public string? ImageUrl { get; init; }
  public string? SiteDetailUrl { get; init; }
  public int[] FriendIds { get; init; } = [];
  public int[] EnemyIds { get; init; } = [];
  public int SeedUseCount { get; set; }
  public int BridgeUseCount { get; set; }
}
