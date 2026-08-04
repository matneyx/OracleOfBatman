using OracleOfBatman.Domain;
using Xunit;

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
}
