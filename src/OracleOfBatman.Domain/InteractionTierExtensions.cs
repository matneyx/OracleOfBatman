namespace OracleOfBatman.Domain;

public static class InteractionTierExtensions
{
    /// <summary>A single past-tense verb fitting "{Character} was {phrase} in {issue} with
    /// {other Character}" (the human-readable hop sentence).</summary>
    public static string ToDisplayPhrase(this InteractionTier tier) => tier switch
    {
        InteractionTier.SharedIdentity => "identified",
        InteractionTier.SameIssue => "credited",
        InteractionTier.MetaMention => "referenced",
        InteractionTier.InUniverseMention => "mentioned",
        InteractionTier.SharedScene => "seen",
        InteractionTier.DirectInteraction => "interacting",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, message: null),
    };

    /// <summary>Whether this tier's meaning is the same regardless of which Character is
    /// Source and which is Target (see CONTEXT.md). Directional tiers (In-Universe Mention,
    /// Meta Mention) must keep their stored direction as meaningful data; Symmetric tiers can
    /// have their Source/Target order canonicalized on write so the same real-world pair never
    /// produces two relationships pointing opposite ways.</summary>
    public static bool IsSymmetric(this InteractionTier tier) => tier switch
    {
        InteractionTier.SharedIdentity => true,
        InteractionTier.SameIssue => true,
        InteractionTier.MetaMention => false,
        InteractionTier.InUniverseMention => false,
        InteractionTier.SharedScene => true,
        InteractionTier.DirectInteraction => true,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, message: null),
    };
}
