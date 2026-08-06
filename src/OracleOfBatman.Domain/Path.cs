namespace OracleOfBatman.Domain;

/// <summary>
///   The ordered sequence of Hops between two queried Characters. BatmanNumber is
///   computed from Hops, not stored separately.
/// </summary>
public sealed record Path(IReadOnlyList<Character> Characters, IReadOnlyList<Hop> Hops)
{
  public int BatmanNumber => Hops.Count;
}
