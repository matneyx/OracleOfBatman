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
}
