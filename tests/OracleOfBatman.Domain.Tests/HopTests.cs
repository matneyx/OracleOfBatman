using OracleOfBatman.Domain;
using Xunit;

namespace OracleOfBatman.Domain.Tests;

public class HopTests
{
    [Fact]
    public void SameFields_AreEqual()
    {
        var softServe = new Character(176719, "Soft Serve");
        var beast = new Character(15694, "Beast");

        var a = new Hop(softServe, beast, 111, InteractionTier.SameIssue, Confidence.Unverified);
        var b = new Hop(softServe, beast, 111, InteractionTier.SameIssue, Confidence.Unverified);

        Assert.Equal(a, b);
    }

    [Fact]
    public void SwappedFromTo_AreNotEqual()
    {
        var softServe = new Character(176719, "Soft Serve");
        var beast = new Character(15694, "Beast");

        var forward = new Hop(softServe, beast, 111, InteractionTier.SameIssue, Confidence.Unverified);
        var backward = new Hop(beast, softServe, 111, InteractionTier.SameIssue, Confidence.Unverified);

        Assert.NotEqual(forward, backward);
    }
}
