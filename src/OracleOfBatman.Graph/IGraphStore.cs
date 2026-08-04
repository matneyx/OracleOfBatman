using OracleOfBatman.Domain;

namespace OracleOfBatman.Graph;

/// <summary>
/// The graph operations ConnectionCrawler (Ingest) and the Path service (Web) need. Pure
/// Neo4j+Domain access, no Comic Vine knowledge (ADR-0011). Neo4jGraphWriter is the real
/// implementation; tests use an in-memory fake so decision logic is verifiable without
/// Docker/Neo4j.
/// </summary>
public interface IGraphStore
{
    Task<bool> PathExistsAsync(int characterAComicVineId, int characterBComicVineId);

    /// <summary>Null covers every "not enough data" case (either character unseeded, or no
    /// path within maxDepth) — undifferentiated, per ADR-0011.</summary>
    Task<Domain.Path?> FindShortestPathAsync(int characterAComicVineId, int characterBComicVineId, int maxDepth);

    Task UpsertCharacterAsync(Character character);

    /// <summary>Persists the raw issue_credits list so future crawls (any pair, any run) can
    /// check overlaps against this character too, not just characters from their own run
    /// (ADR-0012).</summary>
    Task UpsertCharacterIssueCreditsAsync(int comicVineId, IReadOnlyList<int> issueCreditIds);

    /// <summary>Every other Character anywhere in the graph sharing at least one issue with
    /// the given list, keyed by their Comic Vine id, with which issue(s) matched.</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> FindOverlappingIssuesAsync(int comicVineId, IReadOnlyList<int> issueCreditIds);

    Task UpsertConnectionAsync(Connection connection);
}
