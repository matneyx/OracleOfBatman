namespace OracleOfBatman.Domain;

/// <summary>MVP scope cut: 1:1 with a Comic Vine entry, no Mantle/Portrayal/Universe yet (see docs/MVP.md).</summary>
public sealed record Character(int ComicVineId, string Name);
