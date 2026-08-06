using System.Diagnostics;
using Neo4j.Driver;
using OracleOfBatman.Domain;
using Path = OracleOfBatman.Domain.Path;

namespace OracleOfBatman.Graph;

/// <summary>
///   Writes into the schema from ADR-0007:
///   (:Character {comic_vine_id, name})-[:CONNECTION {comic_issue_id, tier, confidence, published_at}]->(:Character)
///   Caller owns the driver's lifetime — this does not dispose it.
/// </summary>
public sealed class Neo4jGraphWriter(IDriver driver, string? database = null) : IGraphStore
{

  public async Task<bool> PathExistsAsync(int characterAComicVineId, int characterBComicVineId)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    return await session.ExecuteReadAsync(async tx =>
    {
      // Undirected: Batman Number pathfinding cares whether any path exists, not the
      // stored relationship direction (which reflects discovery order, not semantics).
      var cursor = await tx.RunAsync(
        """
        MATCH (a:Character {comic_vine_id: $aId})
        MATCH (b:Character {comic_vine_id: $bId})
        RETURN EXISTS { (a)-[:CONNECTION*]-(b) } AS pathExists
        """,
        new { aId = characterAComicVineId, bId = characterBComicVineId });
      var records = await cursor.ToListAsync();
      return records.Count > 0 && records[0]["pathExists"].As<bool>();
    });
  }

  public async Task<Path?> FindShortestPathAsync(int characterAComicVineId, int characterBComicVineId, int maxDepth)
  {
    Debug.Assert(maxDepth > 0, "maxDepth must be positive");

    await using var session = driver.AsyncSession(ConfigureSession);
    return await session.ExecuteReadAsync(async tx =>
    {
      // Variable-length relationship bounds can't be parameterized in Cypher — maxDepth
      // is a validated int, not user text, so interpolating it is safe.
      var cursor = await tx.RunAsync(
        $$"""
          MATCH (a:Character {comic_vine_id: $aId})
          MATCH (b:Character {comic_vine_id: $bId})
          MATCH p = shortestPath((a)-[:CONNECTION*..{{maxDepth}}]-(b))
          RETURN [n IN nodes(p) | {comicVineId: n.comic_vine_id, name: n.name, imageUrl: n.image_url, siteDetailUrl: n.site_detail_url}] AS characters,
                 [r IN relationships(p) | {comicIssueId: r.comic_issue_id, tier: r.tier, confidence: r.confidence, comicIssueName: r.comic_issue_name, comicIssueSiteDetailUrl: r.comic_issue_site_detail_url}] AS hops
          """,
        new { aId = characterAComicVineId, bId = characterBComicVineId });

      var records = await cursor.ToListAsync();
      if (records.Count == 0)
      {
        return null;
      }

      var record = records[0];
      var characterMaps = record["characters"].As<List<object>>();
      var hopMaps = record["hops"].As<List<object>>();

      var characters = characterMaps
        .Select(m => m.As<IReadOnlyDictionary<string, object>>())
        .Select(m => new Character(m["comicVineId"].As<int>(), m["name"].As<string>(), m["imageUrl"].As<string?>(),
          m["siteDetailUrl"].As<string?>()))
        .ToList();

      // Hop.From/To come from walk order (adjacent Characters), not the relationship's
      // stored direction — Same Issue/Shared Scene are Symmetric, so there's nothing to
      // preserve there anyway (ADR-0011).
      var hops = new List<Hop>();
      for (var i = 0; i < hopMaps.Count; i++)
      {
        var hopMap = hopMaps[i].As<IReadOnlyDictionary<string, object>>();
        var comicIssueId = hopMap["comicIssueId"] is null ? (int?)null : hopMap["comicIssueId"].As<int>();
        var tier = Enum.Parse<InteractionTier>(hopMap["tier"].As<string>());
        var confidence = Enum.Parse<Confidence>(hopMap["confidence"].As<string>());
        var comicIssueName = hopMap["comicIssueName"].As<string?>();
        var comicIssueSiteDetailUrl = hopMap["comicIssueSiteDetailUrl"].As<string?>();
        hops.Add(new Hop(characters[i], characters[i + 1], comicIssueId, tier, confidence, comicIssueName,
          comicIssueSiteDetailUrl));
      }

      return new Path(characters, hops);
    });
  }

  public async Task UpsertCharacterAsync(Character character)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    await session.ExecuteWriteAsync(async tx =>
    {
      // coalesce: a ref-only upsert (no image/link known yet) must never blank out
      // enrichment a prior, fuller fetch already stored.
      var cursor = await tx.RunAsync(
        """
        MERGE (c:Character {comic_vine_id: $comicVineId})
        SET c.name = $name,
            c.image_url = coalesce($imageUrl, c.image_url),
            c.site_detail_url = coalesce($siteDetailUrl, c.site_detail_url)
        """,
        new
        {
          comicVineId = character.ComicVineId, name = character.Name, imageUrl = character.ImageUrl,
          siteDetailUrl = character.SiteDetailUrl
        });
      await cursor.ConsumeAsync();
    });
  }

  public async Task UpsertCharacterIssueCreditsAsync(int comicVineId, IReadOnlyList<int> issueCreditIds)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    await session.ExecuteWriteAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        "MATCH (c:Character {comic_vine_id: $comicVineId}) SET c.issue_credits = $issueCreditIds",
        new { comicVineId, issueCreditIds = issueCreditIds.ToArray() });
      await cursor.ConsumeAsync();
    });
  }

  public async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> FindOverlappingIssuesAsync(int comicVineId,
    IReadOnlyList<int> issueCreditIds)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    return await session.ExecuteReadAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        """
        UNWIND $issueCreditIds AS issueId
        MATCH (other:Character)
        WHERE other.comic_vine_id <> $comicVineId AND issueId IN other.issue_credits
        RETURN other.comic_vine_id AS otherId, collect(DISTINCT issueId) AS sharedIssueIds
        """,
        new { comicVineId, issueCreditIds = issueCreditIds.ToArray() });

      var records = await cursor.ToListAsync();
      var result = new Dictionary<int, IReadOnlyList<int>>();
      foreach (var record in records)
      {
        var otherId = record["otherId"].As<int>();
        var sharedIssueIds = record["sharedIssueIds"].As<List<object>>().Select(v => v.As<int>()).ToList();
        result[otherId] = sharedIssueIds;
      }

      return (IReadOnlyDictionary<int, IReadOnlyList<int>>)result;
    });
  }

  public async Task UpsertConnectionAsync(Connection connection)
  {
    // MVP only ever produces per-issue Connections (ADR-0007) — Shared Identity's
    // issue-less Connections are a later ticket, not this crawl path.
    Debug.Assert(connection.ComicIssueId is not null, "expected a comic issue id for this MVP write path");

    // Symmetric tiers (e.g. Same Issue) carry no meaning in which Character is Source vs
    // Target, but MERGE's relationship pattern is directional — without canonicalizing,
    // the same real-world pair discovered in opposite orders by different crawl runs would
    // MERGE onto two different relationships instead of one. Directional tiers (In-Universe
    // Mention, Meta Mention) must NOT be reordered here: their direction is the fact.
    var (sourceId, targetId) = connection.Tier.IsSymmetric() &&
      connection.SourceCharacterComicVineId > connection.TargetCharacterComicVineId
        ? (connection.TargetCharacterComicVineId, connection.SourceCharacterComicVineId)
        : (connection.SourceCharacterComicVineId, connection.TargetCharacterComicVineId);

    await using var session = driver.AsyncSession(ConfigureSession);
    await session.ExecuteWriteAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        """
        MATCH (source:Character {comic_vine_id: $sourceId})
        MATCH (target:Character {comic_vine_id: $targetId})
        MERGE (source)-[r:CONNECTION {comic_issue_id: $comicIssueId}]->(target)
        SET r.tier = $tier, r.confidence = $confidence, r.published_at = $publishedAt,
            r.comic_issue_name = coalesce($comicIssueName, r.comic_issue_name),
            r.comic_issue_site_detail_url = coalesce($comicIssueSiteDetailUrl, r.comic_issue_site_detail_url)
        """,
        new
        {
          sourceId,
          targetId,
          comicIssueId = connection.ComicIssueId,
          tier = connection.Tier.ToString(),
          confidence = connection.Confidence.ToString(),
          publishedAt = connection.ComicIssuePublishedAt?.ToString("yyyy-MM-dd"),
          comicIssueName = connection.ComicIssueName,
          comicIssueSiteDetailUrl = connection.ComicIssueSiteDetailUrl
        });
      await cursor.ConsumeAsync();
    });
  }

  public async Task<IReadOnlyList<Character>> SearchCharactersAsync(string query, int limit = 20)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    return await session.ExecuteReadAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        """
        MATCH (c:Character)
        WHERE toLower(c.name) CONTAINS toLower($query)
        RETURN c.comic_vine_id AS comicVineId, c.name AS name, c.image_url AS imageUrl, c.site_detail_url AS siteDetailUrl
        ORDER BY c.name
        LIMIT $limit
        """,
        new { query, limit });

      var records = await cursor.ToListAsync();
      return (IReadOnlyList<Character>)records
        .Select(r => new Character(r["comicVineId"].As<int>(), r["name"].As<string>(), r["imageUrl"].As<string?>(),
          r["siteDetailUrl"].As<string?>()))
        .ToList();
    });
  }

  private void ConfigureSession(SessionConfigBuilder builder)
  {
    if (database is not null)
    {
      builder.WithDatabase(database);
    }
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
