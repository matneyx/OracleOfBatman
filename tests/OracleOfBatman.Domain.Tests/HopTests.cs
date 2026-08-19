namespace OracleOfBatman.Domain.Tests;

public class HopTests
{
  [Fact]
  public void SameFields_AreEqual()
  {
    var softServe = new Character(176719, "Soft Serve");
    var beast = new Character(15694, "Beast");

    var a = new Hop(softServe, beast, new Issue(111, "Some Issue"));
    var b = new Hop(softServe, beast, new Issue(111, "Some Issue"));

    Assert.Equal(a, b);
  }

  [Fact]
  public void SwappedFromTo_AreNotEqual()
  {
    var softServe = new Character(176719, "Soft Serve");
    var beast = new Character(15694, "Beast");

    var forward = new Hop(softServe, beast, new Issue(111, "Some Issue"));
    var backward = new Hop(beast, softServe, new Issue(111, "Some Issue"));

    Assert.NotEqual(forward, backward);
  }

  [Fact]
  public void ComicIssueNameAndSiteDetailUrl_CanBeSet()
  {
    var softServe = new Character(176719, "Soft Serve");
    var beast = new Character(15694, "Beast");

    var hop = new Hop(softServe, beast,
      new Issue(111, "Some Issue", siteDetailUrl: "https://comicvine.gamespot.com/some-issue/4000-111/"));

    Assert.Equal("Some Issue", hop.Issue.Name);
    Assert.Equal("https://comicvine.gamespot.com/some-issue/4000-111/", hop.Issue.SiteDetailUrl);
  }
}
