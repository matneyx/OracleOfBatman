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
    var visited = new HashSet<int> { characterAComicVineId };
    var queue = new Queue<int>();
    queue.Enqueue(characterAComicVineId);

    while (queue.Count > 0)
    {
      var current = queue.Dequeue();
      var neighbors = await GetNeighborCharactersAsync(current);

      foreach (var neighborId in neighbors.Keys)
      {
        if (neighborId == characterBComicVineId)
        {
          return true;
        }

        if (visited.Add(neighborId))
        {
          queue.Enqueue(neighborId);
        }
      }
    }

    return false;
  }

  public async Task<Path?> FindShortestPathAsync(int characterAComicVineId, int characterBComicVineId, int maxDepth)
  {
    Debug.Assert(maxDepth > 0, "maxDepth must be positive");

    var visited = new HashSet<int> { characterAComicVineId };
    var parent = new Dictionary<int, (int PreviousCharacterId, int IssueId)>();
    var queue = new Queue<(int Id, int Depth)>();
    queue.Enqueue((characterAComicVineId, 0));
    visited.Add(characterAComicVineId);

    while (queue.Count > 0)
    {
      var (current, depth) = queue.Dequeue();
      if (current == characterBComicVineId)
      {
        return await ReconstructPathAsync(characterBComicVineId, parent);
      }

      if (depth >= maxDepth) continue;

      foreach (var (neighborId, issueId) in await GetNeighborCharactersAsync(current))
      {
        if (visited.Add(neighborId))
        {
          parent[neighborId] = (current, issueId);
          queue.Enqueue((neighborId, depth + 1));
        }
      }
    }

    return null;
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

  public async Task<Character?> GetCharacterAsync(int comicVineId)
  {
    await using var session = driver.AsyncSession(ConfigureSession);

    return await session.ExecuteReadAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        "MATCH (c:Character {comic_vine_id: $comicVineId})" +
        "RETURN c.comic_vine_id AS comicVineId, c.name AS name, c.image_url AS imageUrl, c.site_detail_url AS siteDetailUrl",
        new { comicVineId });

      return await cursor
        .Select(r => new Character(r["comicVineId"].As<int>(), r["name"].As<string>(), r["imageUrl"].As<string?>(),
          r["siteDetailUrl"].As<string?>()))
        .SingleOrDefaultAsync();
    });
  }

  public async Task UpsertCharacterIssueCreditsAsync(int comicVineCharacterId, IReadOnlyList<int> issueCreditIds)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    await session.ExecuteWriteAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        "MATCH (c:Character {comic_vine_id: $comicVineId}) SET c.issue_credits = $issueCreditIds",
        new { comicVineId = comicVineCharacterId, issueCreditIds = issueCreditIds.ToArray() });
      await cursor.ConsumeAsync();
    });
  }

  public async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> FindOverlappingIssuesAsync(int comicVineCharacterId,
    IReadOnlyList<int> issueCreditIds)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    return await session.ExecuteWriteAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        """
        UNWIND $issueCreditIds AS issueId
        MATCH (other:Character)
        WHERE other.comic_vine_id <> $comicVineId AND issueId IN other.issue_credits
        MERGE (i:Issue {comic_vine_id: issueId})
        WITH i, other, issueId
        UNWIND (coalesce(i.character_credits, []) + [$comicVineId, other.comic_vine_id]) AS creditId
        WITH i, other, issueId, collect(DISTINCT creditId) AS credits
        SET i.character_credits = credits
        RETURN other.comic_vine_id AS otherId, collect(DISTINCT issueId) AS sharedIssueIds
        """,
        new { comicVineId = comicVineCharacterId, issueCreditIds = issueCreditIds.ToArray() });

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

  public async Task<Issue?> GetIssueAsync(int comicVineId)
  {
    await using var session = driver.AsyncSession(ConfigureSession);

    return await session.ExecuteReadAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        "MATCH (c:Issue {comic_vine_id: $comicVineId})" +
        "RETURN c.comic_vine_id AS comicVineId, c.name AS name, c.image_url AS imageUrl, c.site_detail_url AS siteDetailUrl, c.volume_id AS volumeId, c.volume_name AS volumeName",
        new { comicVineId });

      return await cursor
        .Select(r => new Issue(r["comicVineId"].As<int>(), r["name"].As<string>(), r["imageUrl"].As<string?>(),
          r["siteDetailUrl"].As<string?>(), r["volumeId"]?.As<int>(), r["volumeName"]?.As<string?>()))
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
            c.volume_name = $volumeName
        """,
        new
        {
          comicVineId = issue.ComicVineId, name = issue.Name, imageUrl = issue.ImageUrl,
          siteDetailUrl = issue.SiteDetailUrl, volumeId = issue.VolumeId, volumeName = issue.VolumeName
        });
      await cursor.ConsumeAsync();
    });
  }

  public async Task UpsertConnectionAsync(Connection connection)
  {
    // MVP only ever produces per-issue Connections (ADR-0007) — Shared Identity's
    // issue-less Connections are a later ticket, not this crawl path.
    Debug.Assert(connection.ComicIssueId is not null, "expected a comic issue id for this MVP write path");

    // Same Issue carries no meaning in which Character is Source vs Target, but MERGE's
    // relationship pattern is directional — without canonicalizing, the same real-world
    // pair discovered in opposite orders by different crawl runs would MERGE onto two
    // different relationships instead of one.
    var (sourceId, targetId) =
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
        SET r.published_at = $publishedAt,
            r.comic_issue_name = coalesce($comicIssueName, r.comic_issue_name),
            r.comic_issue_site_detail_url = coalesce($comicIssueSiteDetailUrl, r.comic_issue_site_detail_url)
        """,
        new
        {
          sourceId,
          targetId,
          comicIssueId = connection.ComicIssueId,
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

  private async Task<IReadOnlyDictionary<int, int>> GetNeighborCharactersAsync(int comicVineId)
  {
    await using var session = driver.AsyncSession(ConfigureSession);
    return await session.ExecuteReadAsync(async tx =>
    {
      var cursor = await tx.RunAsync(
        """
        MATCH (c:Character {comic_vine_id: $comicVineId})
        UNWIND c.issue_credits AS issueId
        MATCH (i:Issue {comic_vine_id: issueId})
        UNWIND i.character_credits AS neighborId
        WITH DISTINCT neighborId, issueId
        WHERE neighborId <> $comicVineId
        RETURN neighborId, issueId
        """,
        new { comicVineId });

      var records = await cursor.ToListAsync();
      var result = new Dictionary<int, int>();
      foreach (var record in records)
      {
        result.TryAdd(record["neighborId"].As<int>(), record["issueId"].As<int>());
      }

      return (IReadOnlyDictionary<int, int>)result;
    });
  }

  private async Task<Path> ReconstructPathAsync(int endingCharacterId,
    Dictionary<int, (int PreviousCharacterId, int IssueId)> parent)
  {
    var idPath = new List<int> { endingCharacterId };

    var characters = new Dictionary<int, Character>();
    var endingCharacter = await GetCharacterAsync(endingCharacterId);
    characters.Add(endingCharacterId, endingCharacter);

    var hops = new List<Hop>();

    while (parent.TryGetValue(idPath[^1], out var link))
    {
      var currentId = idPath[^1];
      idPath.Add(link.PreviousCharacterId);

      var newCharacter = await GetCharacterAsync(link.PreviousCharacterId);
      characters[link.PreviousCharacterId] = newCharacter!;

      var issue = await GetIssueAsync(link.IssueId);

      hops.Add(new Hop(characters[link.PreviousCharacterId], characters[currentId], issue!));
    }

    idPath.Reverse();
    hops.Reverse();

    return new Path([.. idPath.Select(id => characters[id])], hops);
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
