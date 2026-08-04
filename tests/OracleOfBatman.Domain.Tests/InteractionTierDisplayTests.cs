using OracleOfBatman.Domain;
using Xunit;

namespace OracleOfBatman.Domain.Tests;

public class InteractionTierDisplayTests
{
    [Theory]
    [InlineData(InteractionTier.SameIssue, "credited")]
    [InlineData(InteractionTier.SharedScene, "seen")]
    [InlineData(InteractionTier.DirectInteraction, "interacting")]
    [InlineData(InteractionTier.InUniverseMention, "mentioned")]
    [InlineData(InteractionTier.MetaMention, "referenced")]
    [InlineData(InteractionTier.SharedIdentity, "identified")]
    public void ToDisplayPhrase_ReturnsAHumanReadableVerb(InteractionTier tier, string expectedPhrase)
    {
        Assert.Equal(expectedPhrase, tier.ToDisplayPhrase());
    }

    [Theory]
    [InlineData(InteractionTier.SharedIdentity, true)]
    [InlineData(InteractionTier.SameIssue, true)]
    [InlineData(InteractionTier.MetaMention, false)]
    [InlineData(InteractionTier.InUniverseMention, false)]
    [InlineData(InteractionTier.SharedScene, true)]
    [InlineData(InteractionTier.DirectInteraction, true)]
    public void IsSymmetric_MatchesCONTEXTDefinitions(InteractionTier tier, bool expectedSymmetric)
    {
        Assert.Equal(expectedSymmetric, tier.IsSymmetric());
    }
}
