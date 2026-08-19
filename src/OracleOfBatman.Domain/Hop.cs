namespace OracleOfBatman.Domain;

/// <summary>
///   One step of a Path: two adjacent Characters plus the Issue that credits them both
///   (ADR-0016). From/To reflect walk order for display, not a directionality claim.
/// </summary>
public sealed record Hop(
  Character From,
  Character To,
  Issue Issue);
