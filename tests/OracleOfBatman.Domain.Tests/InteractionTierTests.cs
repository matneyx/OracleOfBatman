using OracleOfBatman.Domain;
using Xunit;

namespace OracleOfBatman.Domain.Tests;

public class InteractionTierTests
{
    [Theory]
    [InlineData(InteractionTier.SharedIdentity, InteractionTier.MetaMention, InteractionTier.MetaMention)]
    [InlineData(InteractionTier.InUniverseMention, InteractionTier.SharedScene, InteractionTier.SharedScene)]
    [InlineData(InteractionTier.DirectInteraction, InteractionTier.SharedIdentity, InteractionTier.DirectInteraction)]
    [InlineData(InteractionTier.DirectInteraction, InteractionTier.DirectInteraction, InteractionTier.DirectInteraction)]
    [InlineData(InteractionTier.SharedIdentity, InteractionTier.SameIssue, InteractionTier.SameIssue)]
    [InlineData(InteractionTier.SameIssue, InteractionTier.SharedScene, InteractionTier.SharedScene)]
    public void StrongestTierWins_ViaOrdinalMax(InteractionTier a, InteractionTier b, InteractionTier expectedStrongest)
    {
        var strongest = (InteractionTier)Math.Max((int)a, (int)b);

        Assert.Equal(expectedStrongest, strongest);
    }

    [Fact]
    public void OrderedWeakestToStrongest_MatchesContextMdOrdering()
    {
        InteractionTier[] weakestToStrongest =
        [
            InteractionTier.SharedIdentity,
            InteractionTier.SameIssue,
            InteractionTier.MetaMention,
            InteractionTier.InUniverseMention,
            InteractionTier.SharedScene,
            InteractionTier.DirectInteraction,
        ];

        for (var i = 1; i < weakestToStrongest.Length; i++)
        {
            Assert.True((int)weakestToStrongest[i] > (int)weakestToStrongest[i - 1]);
        }
    }
}
