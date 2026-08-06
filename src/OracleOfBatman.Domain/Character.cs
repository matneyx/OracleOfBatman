namespace OracleOfBatman.Domain;

/// <summary>
///   MVP scope cut: 1:1 with a Comic Vine entry, no Mantle/Portrayal/Universe yet (see docs/MVP.md).
///   ImageUrl/SiteDetailUrl come free on the same character fetch the crawl already makes.
/// </summary>
public sealed record Character(int ComicVineId, string Name, string? ImageUrl = null, string? SiteDetailUrl = null);
