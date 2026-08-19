using OracleOfBatman.Domain;
using Path = OracleOfBatman.Domain.Path;

namespace OracleOfBatman.Graph;

/// <summary>
///   The graph operations ConnectionCrawler (Ingest) and the Path service (Web) need. Pure
///   Neo4j+Domain access, no Comic Vine knowledge (ADR-0011). Neo4jGraphWriter is the real
///   implementation; tests use an in-memory fake so decision logic is verifiable without
///   Docker/Neo4j.
/// </summary>
public interface IGraphStore
{
  Task<bool> PathExistsAsync(int characterAComicVineId, int characterBComicVineId);

  /// <summary>
  ///   Null covers every "not enough data" case (either character unseeded, or no
  ///   path within maxDepth) — undifferentiated, per ADR-0011. Native shortestPath()
  ///   traversal over CREDITED_IN edges (ADR-0016), not an application-level BFS.
  ///   On a successful find, bumps BridgeUseCount for every intermediate (non-endpoint)
  ///   Character and PathUseCount for every hop's Issue — every successful call counts,
  ///   including a repeat lookup of an already-known path (ADR-0016).
  /// </summary>
  Task<Path?> FindShortestPathAsync(int characterAComicVineId, int characterBComicVineId, int maxDepth);

  /// <summary>
  ///   Bumps SeedUseCount for both Characters — called once per search attempt (Go-click
  ///   or "Try to find a connection"), regardless of whether a path is ultimately found
  ///   (ADR-0016). Distinct from FindShortestPathAsync's own bridge/path counters, which
  ///   only bump on success.
  /// </summary>
  Task RecordSeedUseAsync(int characterAComicVineId, int characterBComicVineId);

  Task UpsertCharacterAsync(Character character);

  /// <summary>Null if no Character with this Comic Vine id has been persisted yet.</summary>
  Task<Character?> GetCharacterAsync(int comicVineId);

  /// <summary>
  ///   Merges a CREDITED_IN edge (and its target Issue stub node) for every credit,
  ///   unconditionally — no overlap confirmation required (ADR-0016). Each Issue carries
  ///   only what's already free on the Character's own Comic Vine response (id/name/
  ///   site link); an Issue already enriched by a prior call must not have that data
  ///   clobbered by a later, plainer credit for the same id.
  /// </summary>
  Task UpsertCreditedInAsync(int comicVineCharacterId, IReadOnlyList<Issue> issueCredits);

  /// <summary>
  ///   Null if this Issue has never been credited to any ingested Character (ADR-0016)
  ///   — distinct from "known but not yet enriched with an image/Volume".
  /// </summary>
  Task<Issue?> GetIssueAsync(int comicVineId);

  /// <summary>
  ///   Writes enrichment data (name/image/site link/Volume) onto an already-existing
  ///   Issue node — the unified enrichment fetch's write-back step (ADR-0015/0016).
  /// </summary>
  Task UpsertIssueAsync(Issue issue);

  /// <summary>
  ///   Case-insensitive substring match against Character names, ordered
  ///   alphabetically, bounded by limit (MVP ticket 6) — lets the UI resolve typed text to a
  ///   character id.
  /// </summary>
  Task<IReadOnlyList<Character>> SearchCharactersAsync(string query, int limit = 20);

  /// <summary>
  ///   The Character with the oldest IngestionDateTime, excluding the given ids — the
  ///   "Random Character" button's actual mechanism (ADR-0016): a disguised
  ///   least-recently-refreshed picker, not real randomness. Null if every Character is
  ///   excluded, or none exist. A Character with no IngestionDateTime at all (never
  ///   actually ingested) is never a candidate — nothing to refresh.
  /// </summary>
  Task<Character?> GetLeastRecentlyIngestedCharacterAsync(IReadOnlyCollection<int> excludedIds);
}
