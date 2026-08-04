using OracleOfBatman.Domain;

namespace OracleOfBatman.Ingest;

/// <summary>
/// The graph operations ConnectionCrawler needs (ADR-0010). Neo4jGraphWriter is the real
/// implementation; tests use an in-memory fake so the crawl's decision logic is verifiable
/// without Docker/Neo4j.
/// </summary>
public interface IGraphStore
{
    Task<bool> PathExistsAsync(int characterAComicVineId, int characterBComicVineId);

    Task UpsertCharacterAsync(Character character);

    Task UpsertConnectionAsync(Connection connection);
}
