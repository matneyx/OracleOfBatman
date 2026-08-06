namespace OracleOfBatman.Domain;

/// <summary>
///   Ordered weakest (0) to strongest (5) so "strongest tier wins" (ADR-0007) is a Math.Max
///   over ordinal values, not a separate lookup table.
/// </summary>
public enum InteractionTier
{
  SharedIdentity = 0,
  SameIssue = 1,
  MetaMention = 2,
  InUniverseMention = 3,
  SharedScene = 4,
  DirectInteraction = 5
}
