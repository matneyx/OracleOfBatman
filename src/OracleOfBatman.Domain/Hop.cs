namespace OracleOfBatman.Domain;

/// <summary>One step of a Path: two adjacent Characters plus the pair's single representative
/// Connection. From/To reflect walk order for display, not a directionality claim.</summary>
public sealed record Hop(Character From, Character To, int? ComicIssueId, InteractionTier Tier, Confidence Confidence);
