using System.Diagnostics;
using Neo4j.Driver;
using OracleOfBatman.Domain;
using Path = OracleOfBatman.Domain.Path;

namespace OracleOfBatman.Graph;

/// <summary>
///   Writes into the schema from ADR-0016:
///   (:Character {comic_vine_id, name})-[:CREDITED_IN]->(:Issue {comic_vine_id, name})
///   Caller owns the driver's lifetime — this does not dispose it.
/// </summary>
public sealed class Neo4jGraphWriter(IDriver driver, string? database = null) : IGraphStore
{

  public async Task<bool> PathExistsAsync(int characterAComicVineId, int characterBComicVineId)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    var cursor = await session.RunAsync(
      """
      MATCH (a:Character {comic_vine_id: $aId}), (b:Character {comic_vine_id: $bId})
      RETURN EXISTS { (a)-[:CREDITED_IN*]-(b) } AS pathExists
      """,
      new { aId = characterAComicVineId, bId = characterBComicVineId });
    var record = await cursor.SingleOrDefaultAsync();
    return record?["pathExists"].As<bool>() ?? false;
  }

  public async Task<Path?> FindShortestPathAsync(int characterAComicVineId, int characterBComicVineId, int maxDepth)
  {
    Debug.Assert(maxDepth > 0, "maxDepth must be positive");

    await using var session = driver.AsyncSession(ConfigureSession);
    var cursor = await session.RunAsync(
      $"MATCH (a:Character {{comic_vine_id: $aId}}), (b:Character {{comic_vine_id: $bId}})" +
      $"OPTIONAL MATCH p = shortestPath((a)-[:CREDITED_IN*..{maxDepth * 2}]-(b))" +
      $"RETURN p"
       ,
      new { aId = characterAComicVineId, bId = characterBComicVineId });

    var record = await cursor.SingleOrDefaultAsync();

    if (record?["p"] is not IPath path)
    {
      return null;
    }

    var nodes = path.Nodes.ToList();
    var characters = new List<Character>();
    var issues = new List<Issue>();

    for (var i = 0; i < nodes.Count; i++)
    {
      if (i % 2 == 0)
      {
        characters.Add(MapCharacter(nodes[i]));
      }
      else
      {
        issues.Add(MapIssue(nodes[i]));
      }
    }

    var hops = issues.Select((t, i) => new Hop(characters[i], characters[i + 1], t)).ToList();

    // Update usage count on bridge characters and issues
    await session.ExecuteWriteAsync(async tx =>
    {
      var bridgeCursor = await tx.RunAsync(
        """
        UNWIND $bridgeIds AS bridgeId
        MATCH (c:Character {comic_vine_id: bridgeId})
        SET c.bridge_use_count = coalesce(c.bridge_use_count, 0) + 1
        """,
        new { bridgeIds = characters.Skip(1).SkipLast(1).Select(c => c.ComicVineId) });
      await bridgeCursor.ConsumeAsync();

      var issueCursor = await tx.RunAsync(
        """
        UNWIND $issueIds AS issueId
        MATCH (i:Issue {comic_vine_id: issueId})
        SET i.path_use_count = coalesce(i.path_use_count, 0) + 1
        """,
        new { issueIds = issues.Select(i => i.ComicVineId) });
      await issueCursor.ConsumeAsync();
    });

    return new Path(characters, hops);
  }

  public async Task RecordSeedUseAsync(int characterAComicVineId, int characterBComicVineId)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    await session.ExecuteWriteAsync(async tx =>
    {
      await tx.RunAsync(
        """
        MATCH (a:Character {comic_vine_id: $aId}), (b:Character {comic_vine_id: $bId})
        SET a.seed_use_count = coalesce(a.seed_use_count, 0) + 1,
            b.seed_use_count = coalesce(b.seed_use_count, 0) + 1
        """
        ,
        new { aId = characterAComicVineId, bId = characterBComicVineId });
    });
  }

  private Issue MapIssue(INode node) => new(
    node["comic_vine_id"].As<int>(),
    node["name"].As<string>(),
    imageUrl: GetOptionalProperty<string>(node, "image_url"),
    siteDetailUrl: GetOptionalProperty<string>(node, "site_detail_url"),
    volumeId: GetOptionalProperty<int>(node, "volume_id"),
    volumeName: GetOptionalProperty<string>(node, "volume_name"));

  private Character MapCharacter(INode node) => new(
    node["comic_vine_id"].As<int>(),
    node["name"].As<string>(),
    imageUrl: GetOptionalProperty<string>(node, "image_url"),
    siteDetailUrl: GetOptionalProperty<string>(node, "site_detail_url"));

  private static T? GetOptionalProperty<T>(INode node, string propertyName)
    => node.Properties.TryGetValue(propertyName, out var value)
        ? value.As<T?>()
        : default;

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
            c.site_detail_url = coalesce($siteDetailUrl, c.site_detail_url),
            c.friend_ids = $friendIds,
            c.enemy_ids = $enemyIds,
            c.ingestion_date_time = $ingestionDateTime
        """,
        new
        {
          comicVineId = character.ComicVineId, name = character.Name, imageUrl = character.ImageUrl,
          siteDetailUrl = character.SiteDetailUrl, friendIds = character.FriendIds, enemyIds = character.EnemyIds,
          ingestionDateTime = character.IngestionDateTime
        });
      await cursor.ConsumeAsync();
    });
  }

  public async Task<Character?> GetCharacterAsync(int comicVineId)
  {
    await using var session = driver.AsyncSession(ConfigureSession);

    return await session.ExecuteReadAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        "MATCH (c:Character {comic_vine_id: $comicVineId})" +
        "RETURN c.comic_vine_id AS comicVineId, c.name AS name, c.image_url AS imageUrl, c.site_detail_url AS siteDetailUrl, c.seed_use_count AS seedUseCount, c.bridge_use_count AS bridgeUseCount, c.friend_ids AS friendIds, c.enemy_ids AS enemyIds, c.ingestion_date_time AS ingestionDateTime",
        new { comicVineId });

      return await cursor
        .Select(r => new Character(r["comicVineId"].As<int>(),
          r["name"].As<string>(),
          imageUrl: r["imageUrl"].As<string?>(),
          siteDetailUrl: r["siteDetailUrl"].As<string?>(),
          seedUseCount: r["seedUseCount"]?.As<int>() ?? 0,
          bridgeUseCount: r["bridgeUseCount"]?.As<int>() ?? 0,
          friendIds: r["friendIds"] is null ? [] : [.. r["friendIds"].As<List<object>>().Select(v => v.As<int>())],
          enemyIds: r["enemyIds"] is null ? [] : [.. r["enemyIds"].As<List<object>>().Select(v => v.As<int>())],
          ingestionDateTime: r["ingestionDateTime"]?.As<DateTimeOffset>().UtcDateTime
          ))
        .SingleOrDefaultAsync();
    });
  }

  public async Task UpsertCreditedInAsync(int comicVineCharacterId, IReadOnlyList<Issue> issueCredits)
  {
    await using var session = driver.AsyncSession(ConfigureSession);

    foreach (var issue in issueCredits)
    {

      await session.ExecuteWriteAsync(async tx =>
      {
        var cursor = await tx.RunAsync(
          """
          MATCH (c:Character {comic_vine_id: $characterId})
          MERGE (i:Issue {comic_vine_id: $issueId})
            ON CREATE SET i.name = $name, i.site_detail_url = $siteDetailUrl
          MERGE (c)-[:CREDITED_IN]->(i)
          """,
          new { characterId = comicVineCharacterId, issueId = issue.ComicVineId, name = issue.Name, siteDetailUrl = issue.SiteDetailUrl });
        await cursor.ConsumeAsync();
      });
    }
  }

  public async Task<Issue?> GetIssueAsync(int comicVineId)
  {
    await using var session = driver.AsyncSession(ConfigureSession);

    return await session.ExecuteReadAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        "MATCH (c:Issue {comic_vine_id: $comicVineId})" +
        "RETURN c.comic_vine_id AS comicVineId, c.name AS name, c.image_url AS imageUrl, c.site_detail_url AS siteDetailUrl, c.volume_id AS volumeId, c.volume_name AS volumeName, c.path_use_count AS pathUseCount, c.character_credits AS characterCredits",
        new { comicVineId });

      return await cursor
        .Select(r => new Issue(r["comicVineId"].As<int>(),
          r["name"].As<string>(),
          imageUrl: r["imageUrl"].As<string?>(),
          siteDetailUrl: r["siteDetailUrl"].As<string?>(),
          volumeId: r["volumeId"]?.As<int>(),
          volumeName: r["volumeName"]?.As<string?>(),
          pathUseCount: r[ "pathUseCount"]?.As<int>() ?? 0,
          characterCredits: r["characterCredits"] is null ? [] : [.. r["characterCredits"].As<List<object>>().Select(v => v.As<int>())]
          ))
        .SingleOrDefaultAsync();
    });
  }

  public async Task UpsertIssueAsync(Issue issue) {
    await using var session = driver.AsyncSession(ConfigureSession);
    await session.ExecuteWriteAsync(async tx =>
    {
      // coalesce: a ref-only upsert (no image/link known yet) must never blank out
      // enrichment a prior, fuller fetch already stored.
      var cursor = await tx.RunAsync(
        """
        MERGE (c:Issue {comic_vine_id: $comicVineId})
        SET c.name = $name,
            c.image_url = coalesce($imageUrl, c.image_url),
            c.site_detail_url = coalesce($siteDetailUrl, c.site_detail_url),
            c.volume_id = $volumeId,
            c.volume_name = $volumeName,
            c.character_credits = $characterCredits
        """,
        new
        {
          comicVineId = issue.ComicVineId, name = issue.Name, imageUrl = issue.ImageUrl,
          siteDetailUrl = issue.SiteDetailUrl, volumeId = issue.VolumeId, volumeName = issue.VolumeName,
          characterCredits = issue.CharacterCredits
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
      return (IReadOnlyList<Character>)
      [
        .. records.Select(r =>
          new Character(r["comicVineId"].As<int>(),
            r["name"].As<string>(),
            imageUrl: r["imageUrl"].As<string?>(),
            siteDetailUrl: r["siteDetailUrl"].As<string?>()))
      ];
    });
  }

  public async Task<Character?> GetLeastRecentlyIngestedCharacterAsync(IReadOnlyCollection<int> excludedIds)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    return await session.ExecuteReadAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        """
        MATCH (c:Character)
        WHERE c.ingestion_date_time IS NOT NULL AND NOT c.comic_vine_id IN $excludedIds
        RETURN c.comic_vine_id AS comicVineId, c.name AS name, c.image_url AS imageUrl, c.site_detail_url AS siteDetailUrl,
               c.friend_ids AS friendIds, c.enemy_ids AS enemyIds, c.ingestion_date_time AS ingestionDateTime,
               c.seed_use_count AS seedUseCount, c.bridge_use_count AS bridgeUseCount
        ORDER BY c.ingestion_date_time ASC
        LIMIT 1
        """,
        new { excludedIds });

      return await cursor.Select(r =>
        new Character(r["comicVineId"].As<int>(),
          r["name"].As<string>(),
          imageUrl: r["imageUrl"].As<string?>(),
          siteDetailUrl: r["siteDetailUrl"].As<string?>(),
          seedUseCount: r["seedUseCount"]?.As<int>() ?? 0,
          bridgeUseCount: r["bridgeUseCount"]?.As<int>() ?? 0,
          friendIds: r["friendIds"] is null ? [] : [.. r["friendIds"].As<List<object>>().Select(v => v.As<int>())],
          enemyIds: r["enemyIds"] is null ? [] : [.. r["enemyIds"].As<List<object>>().Select(v => v.As<int>())],
          ingestionDateTime: r["ingestionDateTime"]?.As<DateTimeOffset>().UtcDateTime
        )).SingleOrDefaultAsync();
    });
  }

  private void ConfigureSession(SessionConfigBuilder builder)
  {
    if (database is not null)
    {
      builder.WithDatabase(database);
    }
  }

  public async Task<long> GetSummaryAsync()
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    return await session.ExecuteReadAsync(async tx =>
    {
      var cursor = await tx.RunAsync("MATCH (c:Character) RETURN count(DISTINCT c) AS characters");
      var record = await cursor.SingleOrDefaultAsync();
      return record is null ? 0 : record["characters"].As<long>();
    });
  }
}
