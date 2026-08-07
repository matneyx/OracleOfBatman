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
  ///   path within maxDepth) — undifferentiated, per ADR-0011.
  /// </summary>
  Task<Path?> FindShortestPathAsync(int characterAComicVineId, int characterBComicVineId, int maxDepth);

  Task UpsertCharacterAsync(Character character);

  /// <summary>Null if no Character with this Comic Vine id has been persisted yet.</summary>
  Task<Character?> GetCharacterAsync(int comicVineId);

  /// <summary>
  ///   Persists the raw issue_credits list so future crawls (any pair, any run) can
  ///   check overlaps against this character too, not just characters from their own run
  ///   (ADR-0012).
  /// </summary>
  Task UpsertCharacterIssueCreditsAsync(int comicVineCharacterId, IReadOnlyList<int> issueCreditIds);

  /// <summary>
  ///   Every other Character anywhere in the graph sharing at least one issue with the
  ///   given list, keyed by their Comic Vine id, with which issue(s) matched. Also the
  ///   materialization trigger (ADR-0015): a confirmed overlap here is what causes an
  ///   Issue node to actually get created, with both Characters pushed onto its
  ///   character_credits array — an Issue never gets materialized just because one
  ///   Character's own raw issue_credits happens to mention it.
  /// </summary>
  Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> FindOverlappingIssuesAsync(int comicVineCharacterId,
    IReadOnlyList<int> issueCreditIds);

  /// <summary>
  ///   Null if no Issue with this Comic Vine id has been materialized yet (ADR-0015)
  ///   — distinct from "known but not yet enriched with an image/Volume".
  /// </summary>
  Task<Issue?> GetIssueAsync(int comicVineId);

  /// <summary>
  ///   Writes enrichment data (name/image/site link/Volume) onto an already-materialized
  ///   Issue node — the unified enrichment fetch's write-back step (ADR-0015). Never itself
  ///   triggers materialization; the Issue must already exist via a confirmed overlap.
  /// </summary>
  Task UpsertIssueAsync(Issue issue);

  Task UpsertConnectionAsync(Connection connection);

  /// <summary>
  ///   Case-insensitive substring match against Character names, ordered
  ///   alphabetically, bounded by limit (MVP ticket 6) — lets the UI resolve typed text to a
  ///   character id.
  /// </summary>
  Task<IReadOnlyList<Character>> SearchCharactersAsync(string query, int limit = 20);
}
