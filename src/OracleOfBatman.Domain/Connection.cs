namespace OracleOfBatman.Domain;

/// <summary>
///   One atomic per-issue record between two Characters (ADR-0007) — a pair can have many
///   Connections, one per shared issue. ComicIssueId is null only for a Shared Identity
///   Connection, which references no issue. ComicIssueName/SiteDetailUrl come free on the same
///   issue_credits entry the crawl already has (no extra Comic Vine request).
/// </summary>
public sealed record Connection(
  int SourceCharacterComicVineId,
  int TargetCharacterComicVineId,
  int? ComicIssueId,
  DateOnly? ComicIssuePublishedAt,
  string? ComicIssueName = null,
  string? ComicIssueSiteDetailUrl = null);
