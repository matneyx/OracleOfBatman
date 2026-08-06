using Bunit;
using MudBlazor.Services;
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
        var cut = Render<IssueCard>(p => p
            .Add(c => c.Name, "Spoonful of Everything – Part 2!")
            .Add(c => c.SiteDetailUrl, "https://comicvine.gamespot.com/its-jeff-infinity-comic-45-spoonful-of-everything-/4000-1101757/"));

        var link = cut.Find("a");
        Assert.Equal("https://comicvine.gamespot.com/its-jeff-infinity-comic-45-spoonful-of-everything-/4000-1101757/", link.GetAttribute("href"));
        Assert.Contains("Spoonful of Everything – Part 2!", link.TextContent);
        // Comic Vine's page shouldn't navigate the user away from the app entirely.
        Assert.Equal("_blank", link.GetAttribute("target"));
    }

    [Fact]
    public void RendersPlainName_WhenSiteDetailUrlIsAbsent()
    {
        var cut = Render<IssueCard>(p => p.Add(c => c.Name, "Some Issue"));

        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("Some Issue", cut.Markup);
    }

    [Fact]
    public void RendersAnAvatarImage_WhenImageUrlIsPresent()
    {
        var cut = Render<IssueCard>(p => p.Add(c => c.ImageUrl, "https://example.com/issue.jpg"));

        var img = cut.Find("img");
        Assert.Equal("https://example.com/issue.jpg", img.GetAttribute("src"));
    }
}
