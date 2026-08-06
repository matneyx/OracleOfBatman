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
    var character = new Character(157242, "Jeff the Land Shark", "https://example.com/jeff.jpg",
      "https://comicvine.gamespot.com/jeff-the-land-shark/4005-157242/");

    Assert.Equal("https://example.com/jeff.jpg", character.ImageUrl);
    Assert.Equal("https://comicvine.gamespot.com/jeff-the-land-shark/4005-157242/", character.SiteDetailUrl);
  }
}
