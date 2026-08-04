using OracleOfBatman.Domain;
using OracleOfBatman.Graph.ComicVine;
using Xunit;

namespace OracleOfBatman.Graph.Tests;

public class ComicVineMapperTests
{
    [Fact]
    public void ComicVineCharacter_MapsToDomainCharacter()
    {
        var source = new ComicVineCharacter { Id = 12605, Name = "Jim Hammond" };

        var character = source.ToDomain();

        Assert.Equal(new Character(12605, "Jim Hammond"), character);
    }

    [Fact]
    public void ComicVineCharacter_MapsImageAndSiteDetailUrl()
    {
        var source = new ComicVineCharacter
        {
            Id = 12605,
            Name = "Jim Hammond",
            SiteDetailUrl = "https://comicvine.gamespot.com/jim-hammond/4005-12605/",
            Image = new ComicVineImage { IconUrl = "https://example.com/jim-icon.jpg" },
        };

        var character = source.ToDomain();

        Assert.Equal("https://example.com/jim-icon.jpg", character.ImageUrl);
        Assert.Equal("https://comicvine.gamespot.com/jim-hammond/4005-12605/", character.SiteDetailUrl);
    }

    [Fact]
    public void ComicVineCharacterRef_MapsToDomainCharacter()
    {
        var source = new ComicVineCharacterRef { Id = 125054, Name = "Gwenpool" };

        var character = source.ToDomain();

        Assert.Equal(new Character(125054, "Gwenpool"), character);
    }
}
