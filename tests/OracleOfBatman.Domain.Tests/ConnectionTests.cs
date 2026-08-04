using OracleOfBatman.Domain;
using Xunit;

namespace OracleOfBatman.Domain.Tests;

public class ConnectionTests
{
    [Fact]
    public void SameFields_AreEqual()
    {
        var a = new Connection(125054, 157242, 1101757, new DateOnly(2025, 4, 4), InteractionTier.SharedScene, Confidence.Unverified);
        var b = new Connection(125054, 157242, 1101757, new DateOnly(2025, 4, 4), InteractionTier.SharedScene, Confidence.Unverified);

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentComicIssueId_AreNotEqual()
    {
        var firstIssue = new Connection(125054, 157242, 1101757, null, InteractionTier.SharedScene, Confidence.Unverified);
        var secondIssue = new Connection(125054, 157242, 1101758, null, InteractionTier.SharedScene, Confidence.Unverified);

        Assert.NotEqual(firstIssue, secondIssue);
    }

    [Fact]
    public void ComicIssueNameAndSiteDetailUrl_DefaultToNull()
    {
        var connection = new Connection(125054, 157242, 1101757, null, InteractionTier.SameIssue, Confidence.Unverified);

        Assert.Null(connection.ComicIssueName);
        Assert.Null(connection.ComicIssueSiteDetailUrl);
    }

    [Fact]
    public void ComicIssueNameAndSiteDetailUrl_CanBeSet()
    {
        var connection = new Connection(
            125054, 157242, 1101757, null, InteractionTier.SameIssue, Confidence.Unverified,
            "Spoonful of Everything – Part 2!",
            "https://comicvine.gamespot.com/its-jeff-infinity-comic-45-spoonful-of-everything-/4000-1101757/");

        Assert.Equal("Spoonful of Everything – Part 2!", connection.ComicIssueName);
        Assert.Equal("https://comicvine.gamespot.com/its-jeff-infinity-comic-45-spoonful-of-everything-/4000-1101757/", connection.ComicIssueSiteDetailUrl);
    }
}
