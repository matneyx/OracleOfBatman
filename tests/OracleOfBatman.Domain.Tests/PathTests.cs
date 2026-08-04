using OracleOfBatman.Domain;
using Xunit;

namespace OracleOfBatman.Domain.Tests;

public class PathTests
{
    [Fact]
    public void BatmanNumber_IsHopCount_NotStoredSeparately()
    {
        var softServe = new Character(176719, "Soft Serve");
        var beast = new Character(15694, "Beast");
        var bloodscream = new Character(15734, "Bloodscream");

        var path = new Path(
            [softServe, beast, bloodscream],
            [
                new Hop(softServe, beast, 111, InteractionTier.SameIssue, Confidence.Unverified),
                new Hop(beast, bloodscream, 222, InteractionTier.SameIssue, Confidence.Unverified),
            ]);

        Assert.Equal(2, path.BatmanNumber);
    }

    [Fact]
    public void DirectConnection_HasBatmanNumberOne()
    {
        var jimHammond = new Character(12605, "Jim Hammond");
        var jeff = new Character(157242, "Jeff the Land Shark");

        var path = new Path(
            [jimHammond, jeff],
            [new Hop(jimHammond, jeff, 739613, InteractionTier.SameIssue, Confidence.Unverified)]);

        Assert.Equal(1, path.BatmanNumber);
    }
}
