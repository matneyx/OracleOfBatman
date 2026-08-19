using Xunit;

namespace OracleOfBatman.Domain.Tests;

public class IssueDisplayTests
{
    [Fact]
    public void ToDisplayName_CombinesVolumeAndName_WhenBothArePresent()
    {
        var issue = new Issue(1, "Spoonful of Everything – Part 2!", volumeId: 9, volumeName: "It's Jeff Infinity Comic");

        Assert.Equal("It's Jeff Infinity Comic: Spoonful of Everything – Part 2!", issue.ToDisplayName());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ToDisplayName_ShowsVolumeAlone_WhenNameIsBlank(string? blankName)
    {
        var issue = new Issue(1, blankName, volumeId: 9, volumeName: "It's Jeff Infinity Comic");

        Assert.Equal("It's Jeff Infinity Comic", issue.ToDisplayName());
    }

    [Fact]
    public void ToDisplayName_ShowsNameAlone_WhenVolumeIsNotYetKnown()
    {
        // Enrichment hasn't happened yet (ADR-0015) — degrade gracefully rather than
        // showing nothing.
        var issue = new Issue(1, "Some Issue");

        Assert.Equal("Some Issue", issue.ToDisplayName());
    }

    [Fact]
    public void ToDisplayName_IsBlank_WhenNeitherNameNorVolumeIsKnown()
    {
        var issue = new Issue(1, null);

        Assert.Equal("", issue.ToDisplayName());
    }

    [Fact]
    public void ToDisplayName_CombinesVolumeAndTpb_LikeAnyOtherName()
    {
        // "TPB" needs no special-casing (ADR-0015) — it's just a normal Name value that
        // gets combined with the Volume the same as any other.
        var issue = new Issue(1, "TPB", volumeId: 9, volumeName: "It's Jeff Infinity Comic");

        Assert.Equal("It's Jeff Infinity Comic: TPB", issue.ToDisplayName());
    }
}
