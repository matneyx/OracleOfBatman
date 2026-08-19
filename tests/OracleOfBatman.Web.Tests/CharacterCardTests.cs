using Bunit;
using MudBlazor.Services;
using OracleOfBatman.Domain;
using OracleOfBatman.Web.Components.Shared;

namespace OracleOfBatman.Web.Tests;

public class CharacterCardTests : BunitContext
{
  public CharacterCardTests()
  {
    Services.AddMudServices();
  }

  [Fact]
  public void RendersNameAsALink_WhenSiteDetailUrlIsPresent()
  {
    var character = new Character(12605, "Jim Hammond", imageUrl: null, siteDetailUrl: "https://comicvine.gamespot.com/jim-hammond/4005-12605/");

    var cut = Render<CharacterCard>(p => p.Add(c => c.Character, character));

    var link = cut.Find("a");
    Assert.Equal("https://comicvine.gamespot.com/jim-hammond/4005-12605/", link.GetAttribute("href"));
    Assert.Contains("Jim Hammond", link.TextContent);
    // Comic Vine's page shouldn't navigate the user away from the app entirely.
    Assert.Equal("_blank", link.GetAttribute("target"));
  }

  [Fact]
  public void RendersPlainName_WhenSiteDetailUrlIsAbsent()
  {
    var character = new Character(12605, "Jim Hammond");

    var cut = Render<CharacterCard>(p => p.Add(c => c.Character, character));

    Assert.Empty(cut.FindAll("a"));
    Assert.Contains("Jim Hammond", cut.Markup);
  }

  [Fact]
  public void RendersAnAvatarImage_WhenImageUrlIsPresent()
  {
    var character = new Character(12605, "Jim Hammond", imageUrl: "https://example.com/jim.jpg");

    var cut = Render<CharacterCard>(p => p.Add(c => c.Character, character));

    var img = cut.Find("img");
    Assert.Equal("https://example.com/jim.jpg", img.GetAttribute("src"));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("https://comicvine.gamespot.com/jim-hammond/4005-12605/")]
  public void NameElementHasATestId_RegardlessOfWhetherItsALinkOrPlainText(string? siteDetailUrl)
  {
    var character = new Character(12605, "Jim Hammond", imageUrl: null, siteDetailUrl: siteDetailUrl);

    var cut = Render<CharacterCard>(p => p.Add(c => c.Character, character));

    Assert.NotEmpty(cut.FindAll("[data-testid='character-name']"));
  }

  [Fact]
  public void AvatarElementHasATestId()
  {
    var character = new Character(12605, "Jim Hammond", imageUrl: "https://example.com/jim.jpg");

    var cut = Render<CharacterCard>(p => p.Add(c => c.Character, character));

    Assert.NotEmpty(cut.FindAll("[data-testid='character-avatar']"));
  }
}
