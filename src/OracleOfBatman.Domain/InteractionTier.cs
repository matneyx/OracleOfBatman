namespace OracleOfBatman.Domain;

/// <summary>
/// Ordered weakest (0) to strongest (4) so "strongest tier wins" (ADR-0007) is a Math.Max
/// over ordinal values, not a separate lookup table.
/// </summary>
public enum InteractionTier
{
    SharedIdentity = 0,
    MetaMention = 1,
    InUniverseMention = 2,
    SharedScene = 3,
    DirectInteraction = 4,
}
