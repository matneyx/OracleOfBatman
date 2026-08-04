using System.Diagnostics;
using Neo4j.Driver;
using OracleOfBatman.Domain;

namespace OracleOfBatman.Ingest;

/// <summary>
/// Writes into the schema from ADR-0007:
/// (:Character {comic_vine_id, name})-[:CONNECTION {comic_issue_id, tier, confidence, published_at}]->(:Character)
/// Caller owns the driver's lifetime — this does not dispose it.
/// </summary>
public sealed class Neo4jGraphWriter(IDriver driver, string? database = null)
{
    private void ConfigureSession(SessionConfigBuilder builder)
    {
        if (database is not null)
        {
            builder.WithDatabase(database);
        }
    }

    public async Task UpsertCharacterAsync(Character character)
    {
        await using var session = driver.AsyncSession(ConfigureSession);
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MERGE (c:Character {comic_vine_id: $comicVineId}) SET c.name = $name",
                new { comicVineId = character.ComicVineId, name = character.Name });
            await cursor.ConsumeAsync();
        });
    }

    public async Task UpsertConnectionAsync(Connection connection)
    {
        // MVP only ever produces per-issue Connections (ADR-0007) — Shared Identity's
        // issue-less Connections are a later ticket, not this crawl path.
        Debug.Assert(connection.ComicIssueId is not null, "expected a comic issue id for this MVP write path");

        await using var session = driver.AsyncSession(ConfigureSession);
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (source:Character {comic_vine_id: $sourceId})
                MATCH (target:Character {comic_vine_id: $targetId})
                MERGE (source)-[r:CONNECTION {comic_issue_id: $comicIssueId}]->(target)
                SET r.tier = $tier, r.confidence = $confidence, r.published_at = $publishedAt
                """,
                new
                {
                    sourceId = connection.SourceCharacterComicVineId,
                    targetId = connection.TargetCharacterComicVineId,
                    comicIssueId = connection.ComicIssueId,
                    tier = connection.Tier.ToString(),
                    confidence = connection.Confidence.ToString(),
                    publishedAt = connection.ComicIssuePublishedAt?.ToString("yyyy-MM-dd"),
                });
            await cursor.ConsumeAsync();
        });
    }

    public async Task<(long CharacterCount, long ConnectionCount)> GetSummaryAsync()
    {
        await using var session = driver.AsyncSession(ConfigureSession);
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (c:Character) OPTIONAL MATCH ()-[r:CONNECTION]->() " +
                "RETURN count(DISTINCT c) AS characters, count(DISTINCT r) AS connections");
            var record = await cursor.SingleAsync();
            return (record["characters"].As<long>(), record["connections"].As<long>());
        });
    }
}
