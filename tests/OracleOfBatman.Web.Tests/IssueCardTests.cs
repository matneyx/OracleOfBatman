using Bunit;
using MudBlazor.Services;
using OracleOfBatman.Domain;
using OracleOfBatman.Web.Components.Shared;

namespace OracleOfBatman.Web.Tests;

public class IssueCardTests : BunitContext
{
  public IssueCardTests()
  {
    Services.AddMudServices();
  }

  [Fact]
  public void RendersNameAsALink_WhenSiteDetailUrlIsPresent()
  {
    var issue = new Issue(1, "Spoonful of Everything – Part 2!", imageUrl: null,
      siteDetailUrl: "https://comicvine.gamespot.com/its-jeff-infinity-comic-45-spoonful-of-everything-/4000-1101757/");

    var cut = Render<IssueCard>(p => p
      .Add(c => c.Issue, issue));

    var link = cut.Find("a");
    Assert.Equal("https://comicvine.gamespot.com/its-jeff-infinity-comic-45-spoonful-of-everything-/4000-1101757/",
      link.GetAttribute("href"));
    Assert.Contains("Spoonful of Everything – Part 2!", link.TextContent);
    // Comic Vine's page shouldn't navigate the user away from the app entirely.
    Assert.Equal("_blank", link.GetAttribute("target"));
  }

  [Fact]
  public void RendersPlainName_WhenSiteDetailUrlIsAbsent()
  {
    var issue = new Issue(1, "Some Issue");

    var cut = Render<IssueCard>(p => p.Add(c => c.Issue, issue));

    Assert.Empty(cut.FindAll("a"));
    Assert.Contains("Some Issue", cut.Markup);
  }

  [Fact]
  public void RendersAnAvatarImage_WhenImageUrlIsPresent()
  {
    var issue = new Issue(1, string.Empty, imageUrl: "https://example.com/issue.jpg");

    var cut = Render<IssueCard>(p => p.Add(c => c.Issue, issue));

    var img = cut.Find("img");
    Assert.Equal("https://example.com/issue.jpg", img.GetAttribute("src"));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("https://comicvine.gamespot.com/its-jeff-infinity-comic-45-spoonful-of-everything-/4000-1101757/")]
  public void NameElementHasATestId_RegardlessOfWhetherItsALinkOrPlainText(string? siteDetailUrl)
  {
    var issue = new Issue(1, "Spoonful of Everything – Part 2!", imageUrl: null, siteDetailUrl: siteDetailUrl);

    var cut = Render<IssueCard>(p => p
      .Add(c => c.Issue, issue));

    Assert.NotEmpty(cut.FindAll("[data-testid='issue-name']"));
  }

  [Fact]
  public void AvatarElementHasATestId()
  {
    var issue = new Issue(1, null, imageUrl: "https://example.com/issue.jpg");

    var cut = Render<IssueCard>(p => p.Add(c => c.Issue, issue));

    Assert.NotEmpty(cut.FindAll("[data-testid='issue-avatar']"));
  }

  [Fact]
  public void RendersTheCombinedVolumeAndNameDisplayString_WhenVolumeIsKnown()
  {
    // ADR-0015: always show the Volume once known, via Issue.ToDisplayName() — not the
    // raw Name alone.
    var issue = new Issue(1, "Spoonful of Everything – Part 2!", volumeId: 9,
      volumeName: "It's Jeff Infinity Comic");

    var cut = Render<IssueCard>(p => p.Add(c => c.Issue, issue));

    var nameElement = cut.Find("[data-testid='issue-name']");
    Assert.Contains("It's Jeff Infinity Comic: Spoonful of Everything – Part 2!", nameElement.TextContent);
  }
}
