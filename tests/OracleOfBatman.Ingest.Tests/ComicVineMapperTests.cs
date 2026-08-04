using OracleOfBatman.Domain;
using OracleOfBatman.Ingest.ComicVine;
using Xunit;

namespace OracleOfBatman.Ingest.Tests;

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
    public void ComicVineCharacterRef_MapsToDomainCharacter()
    {
        var source = new ComicVineCharacterRef { Id = 125054, Name = "Gwenpool" };

        var character = source.ToDomain();

        Assert.Equal(new Character(125054, "Gwenpool"), character);
    }
}
