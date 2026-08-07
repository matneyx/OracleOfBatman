namespace OracleOfBatman.Graph.ComicVine;

/// <summary>
///   Fetches a single issue by Comic Vine id. Used by the unified enrichment fetch
///   (ADR-0015) to lazily enrich a *displayed* Path's hops with an image/Volume/name — only for
///   the handful of issues actually shown, never eagerly for every materialized Issue.
/// </summary>
public interface IComicVineIssueSource
{
  Task<ComicVineIssue> GetIssueAsync(int comicVineId);
}
