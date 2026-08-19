namespace OracleOfBatman.Domain.Tests;

public class CharacterTests
{
  [Fact]
  public void SameComicVineIdAndName_AreEqual()
  {
    var a = new Character(157242, "Jeff the Land Shark");
    var b = new Character(157242, "Jeff the Land Shark");

    Assert.Equal(a, b);
  }

  [Fact]
  public void DifferentComicVineId_AreNotEqual()
  {
    var jimHammond = new Character(12605, "Jim Hammond");
    var jeff = new Character(157242, "Jeff the Land Shark");

    Assert.NotEqual(jimHammond, jeff);
  }

  [Fact]
  public void ImageUrlAndSiteDetailUrl_DefaultToNull()
  {
    var character = new Character(157242, "Jeff the Land Shark");

    Assert.Null(character.ImageUrl);
    Assert.Null(character.SiteDetailUrl);
  }

  [Fact]
  public void ImageUrlAndSiteDetailUrl_CanBeSet()
  {
    var character = new Character(157242, "Jeff the Land Shark", imageUrl: "https://example.com/jeff.jpg",
      siteDetailUrl: "https://comicvine.gamespot.com/jeff-the-land-shark/4005-157242/");

    Assert.Equal("https://example.com/jeff.jpg", character.ImageUrl);
    Assert.Equal("https://comicvine.gamespot.com/jeff-the-land-shark/4005-157242/", character.SiteDetailUrl);
  }

  [Fact]
  public void IngestionDate_DefaultsToNull()
  {
    // Never-yet-ingested construction (e.g. a bare reference built before a real fetch) has
    // no ingestion date yet (ADR-0016).
    var character = new Character(157242, "Jeff the Land Shark");

    Assert.Null(character.IngestionDateTime);
  }

  [Fact]
  public void IngestionDate_CanBeSet()
  {
    var controlDateTime = DateTime.Now;

    var character = new Character(157242, "Jeff the Land Shark", ingestionDateTime: controlDateTime);

    Assert.Equal(controlDateTime, character.IngestionDateTime);
  }

  [Fact]
  public void FriendIdsAndEnemyIds_DefaultToEmpty()
  {
    // Discovery-only data (ADR-0016) — free on the same ingest response, but a Character
    // with none reported shouldn't need null-checking at every call site.
    var character = new Character(157242, "Jeff the Land Shark");

    Assert.Empty(character.FriendIds);
    Assert.Empty(character.EnemyIds);
  }

  [Fact]
  public void FriendIdsAndEnemyIds_CanBeSet()
  {
    var character = new Character(157242, "Jeff the Land Shark", friendIds: [12605], enemyIds: [125054]);

    Assert.Equal([12605], character.FriendIds);
    Assert.Equal([125054], character.EnemyIds);
  }

  [Fact]
  public void SeedUseCountAndBridgeUseCount_DefaultToZero()
  {
    var character = new Character(157242, "Jeff the Land Shark");

    Assert.Equal(0, character.SeedUseCount);
    Assert.Equal(0, character.BridgeUseCount);
  }

  [Fact]
  public void SeedUseCountAndBridgeUseCount_CanBeSet()
  {
    var character = new Character(157242, "Jeff the Land Shark", seedUseCount: 3, bridgeUseCount: 5);

    Assert.Equal(3, character.SeedUseCount);
    Assert.Equal(5, character.BridgeUseCount);
  }
}
