namespace OracleOfBatman.Graph.ComicVine;

/// <summary>
///   Fetches a single issue by Comic Vine id. Used to lazily enrich a *displayed*
///   Path's hops with an issue thumbnail (only for the handful of issues actually shown), and by
///   ConnectionCrawler during ingestion to resolve a Volume name when an issue's own name is
///   blank or the generic "TPB" (see ADR-0010).
/// </summary>
public interface IComicVineIssueSource
{
  Task<ComicVineIssue> GetIssueAsync(int comicVineId);
}
