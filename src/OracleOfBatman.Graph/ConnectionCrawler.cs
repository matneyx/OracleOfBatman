using OracleOfBatman.Domain;
using OracleOfBatman.Graph.ComicVine;

namespace OracleOfBatman.Graph;

/// <summary>
///   Character and Issue fetches are reported separately but spend one shared budget: an
///   /issue/{id}/ cast-fetch costs exactly what a /character/{id}/ fetch does, even though it
///   reveals many candidates at once (ADR-0014's one unified counter).
/// </summary>
public sealed record CrawlResult(bool Connected, int CharactersFetched, int IssuesFetched = 0);

/// <summary>
///   Implements ADR-0010's bidirectional friend/enemy-BFS crawl: free existing-path pre-check,
///   then direct issue/friend-enemy overlap checks, then budget-bounded bidirectional BFS
///   (smaller-frontier-first). Overlap checks go through the graph itself (ADR-0012), covering
///   every character ever persisted — not just ones discovered in this run. Once friends and
///   enemies are exhausted with no path found, escalates to issue-cast bridge discovery
///   (ADR-0014 steps 4-5).
/// </summary>
public sealed class ConnectionCrawler(
  IComicVineCharacterSource characterSource,
  IGraphStore graphStore,
  IssueEnrichmentService issueEnrichmentService,
  TimeProvider? timeProvider = null,
  Action<string>? trace = null)
{
  /// <summary>
  ///   ADR-0014 step 5: a character is only worth its own /character/{id}/ request once it
  ///   turns up in this many separate issue casts — otherwise one big-ensemble issue would
  ///   drain the whole budget by itself.
  /// </summary>
  private const int StrongCandidateCastAppearancesMin = 2;

  /// <summary>
  ///   Fixed upper bound on the escalation loop (STYLE.md). Budget alone can't bound it: a
  ///   cast already materialized in Neo4j costs nothing, so a graph full of enriched Issues
  ///   would let escalation walk indefinitely without ever spending a unit.
  /// </summary>
  private const int EscalationIssueCastsMax = 200;

  private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
  private readonly Queue<int> _frontierA = [];
  private readonly Queue<int> _frontierB = [];
  private readonly HashSet<int> _visited = [];

  // Every Issue any ingested character is credited in — the pool escalation draws from,
  // so it covers the seeds and everything the BFS pulled in, not just one phase's finds.
  private readonly HashSet<int> _discoveredIssueIds = [];

  // PickRandomCharacterAsync alternates oldest-ingested and truly random picks; circuit-
  // scoped like _shownViaRandom, so a page refresh starts over with an oldest pick.
  private bool _pickOldest = true;

  /// <summary>
  ///   Optional narration of what the crawl is doing and what each step costs — off unless
  ///   a host supplies a sink (Console.WriteLine, an ILogger, ...). Deliberately a callback
  ///   rather than an ILogger so this assembly keeps its two dependencies (STYLE.md).
  /// </summary>
  private void Trace(string message) => trace?.Invoke(message);

  public async Task<CrawlResult> PopulateConnectionsAsync(int seedAComicVineId, int seedBComicVineId, int budget,
    CancellationToken token)
  {
    token.ThrowIfCancellationRequested();

    Trace($"Crawl start: seeds {seedAComicVineId} and {seedBComicVineId}, budget {budget}.");

    if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId, token))
    {
      Trace("Already connected in the graph — nothing to fetch.");
      return new CrawlResult(true, 0);
    }

    // The two seed fetches aren't counted against the expansion budget — the budget is
    // for new characters discovered beyond the seeds (ADR-0010).
    var seedATask = IngestCharacterAsync(seedAComicVineId, token);
    var seedBTask = IngestCharacterAsync(seedBComicVineId, token);
    await Task.WhenAll(seedATask, seedBTask);

    var seedA = seedATask.Result;
    var seedB = seedBTask.Result;

    if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId, token))
    {
      Trace("Seeds share an issue directly — connected after the two seed fetches.");
      return new CrawlResult(true, 0);
    }
    Trace($"Path does not exist between {seedA.Name} (id {seedA.Id}) and {seedB.Name} (id {seedB.Id}); escalating to search friends and enemies");

    EnqueueFrontier(_frontierA, seedA);
    EnqueueFrontier(_frontierB, seedB);

    var (connected, fetched) =
      await PopulateFromCommonNeighborsAsync(seedAComicVineId, seedBComicVineId, budget, token);
    if (connected)
    {
      Trace($"Connected via a shared friend/enemy. Spent {fetched} character fetch(es).");
      return new CrawlResult(true, fetched);
    }
    Trace("Path does not exist between the seeds after searching common neighbors. Escalting to BFS for friends and enemies.");

    (connected, fetched) =
      await PopulateFromBidirectionalBfsAsync(seedAComicVineId, seedBComicVineId, budget, fetched, token);
    if (connected)
    {
      Trace($"Connected via the friend/enemy BFS. Spent {fetched} character fetch(es).");
      return new CrawlResult(true, fetched);
    }
    Trace($"Path does not exist between {seedA.Name} (id {seedA.Id}) and {seedB.Name} (id {seedB.Id}) after BFS; escalating to issue casts");

    // Friends and enemies are exhausted (or never existed) — the only signal left is who
    // else is credited in the issues discovered so far (ADR-0014 steps 4-5).
    var result = await EscalateViaIssueCastsAsync(seedAComicVineId, seedBComicVineId, budget, fetched, token);

    Trace(result.Connected
      ? $"Connected via issue-cast escalation. Spent {result.CharactersFetched} character fetch(es) and {result.IssuesFetched} issue cast(s)."
      : $"No connection found. Spent {result.CharactersFetched} character fetch(es) and {result.IssuesFetched} issue cast(s) of a {budget} budget.");

    return result;
  }

  /// <summary>
  ///   Direct friend/enemy overlap between the seeds: the cheapest, highest-confidence
  ///   bridge candidates, so they're spent on first.
  /// </summary>
  private async Task<(bool Connected, int CharactersFetched)> PopulateFromCommonNeighborsAsync(int seedAComicVineId,
    int seedBComicVineId, int budget, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();

    var fetched = 0;
    var common = _frontierA.Intersect(_frontierB).ToList();
    Trace($"Direct friend/enemy overlap: {common.Count} shared candidate(s).");

    foreach (var candidateId in common)
    {
      if (fetched >= budget)
      {
        Trace("Budget exhausted during the shared friend/enemy phase.");
        return (false, fetched);
      }

      if (_visited.Contains(candidateId))
      {
        continue;
      }

      var sharedFriend = await TryIngestCandidateAsync(candidateId, token);
      fetched++;
      if (sharedFriend is null)
      {
        continue;
      }

      if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId, token))
      {
        return (true, fetched);
      }
    }

    return (false, fetched);
  }

  /// <summary>
  ///   ADR-0010's bidirectional BFS: expand whichever frontier is smaller, one new
  ///   character at a time.
  /// </summary>
  private async Task<(bool Connected, int CharactersFetched)> PopulateFromBidirectionalBfsAsync(int seedAComicVineId,
    int seedBComicVineId, int budget, int fetched, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();
    Trace($"Bidirectional BFS: frontier A has {_frontierA.Count}, frontier B has {_frontierB.Count}.");

    while (fetched < budget && (_frontierA.Count > 0 || _frontierB.Count > 0))
    {
      var side = ChooseSideToExpand();
      var candidateId = DequeueNextUnvisited(side);
      if (candidateId is null)
      {
        continue;
      }

      var newCharacter = await TryIngestCandidateAsync(candidateId.Value, token);
      fetched++;
      if (newCharacter is null)
      {
        continue;
      }

      Trace($"  BFS expanded side {side} to {newCharacter.Name} ({candidateId}) [{fetched}/{budget}].");
      EnqueueFrontier(side == Side.A ? _frontierA : _frontierB, newCharacter);

      if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId, token))
      {
        return (true, fetched);
      }
    }

    return (false, fetched);
  }

  /// <summary>
  ///   Pulls the full cast of each discovered Issue and ingests every character credited in
  ///   StrongCandidateCastAppearancesMin or more of them. A newly-ingested candidate's own
  ///   issues join the pool, so this keeps walking outward until the shared budget runs out.
  /// </summary>
  private async Task<CrawlResult> EscalateViaIssueCastsAsync(int seedAComicVineId, int seedBComicVineId,
    int budget, int charactersFetched, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();

    var issuesFetched = 0;
    var pendingIssueIds = new Queue<int>(_discoveredIssueIds);
    var queuedIssueIds = new HashSet<int>(pendingIssueIds);
    var castAppearances = new Dictionary<int, int>();
    var castsExamined = 0;

    Trace($"Escalating to issue casts (ADR-0014 step 4): {pendingIssueIds.Count} known issue(s) to walk, "
      + $"{budget - charactersFetched} budget left.");

    while (pendingIssueIds.Count > 0
      && castsExamined < EscalationIssueCastsMax
      && charactersFetched + issuesFetched < budget)
    {
      castsExamined++;
      var issueId = pendingIssueIds.Dequeue();
      var issue = await graphStore.GetIssueAsync(issueId);
      if (issue is null)
      {
        Trace($"  Issue {issueId} isn't in the graph — skipping.");
        continue;
      }

      // A cast already in Neo4j (from some earlier enrichment) is free — ADR-0015's
      // refinement of ADR-0014 step 4: never pay Comic Vine for the same cast twice.
      var castWasKnown = issue.CharacterCredits.Length > 0;
      if (!castWasKnown)
      {
        issue = await issueEnrichmentService.EnrichIfNeededAsync(issue, token);
        issuesFetched++;
      }

      Trace($"  Issue {issueId} \"{issue.ToDisplayName()}\": {issue.CharacterCredits.Length} credited character(s)"
        + $" ({(castWasKnown ? "already in the graph, free" : "fetched from Comic Vine")}).");

      foreach (var candidateId in issue.CharacterCredits)
      {
        if (_visited.Contains(candidateId))
        {
          continue;
        }

        castAppearances[candidateId] = castAppearances.GetValueOrDefault(candidateId) + 1;
        if (castAppearances[candidateId] != StrongCandidateCastAppearancesMin)
        {
          continue;
        }

        if (charactersFetched + issuesFetched >= budget)
        {
          Trace($"    Budget exhausted before character {candidateId} could be fetched.");
          return new CrawlResult(false, charactersFetched, issuesFetched);
        }

        Trace($"    Character {candidateId} is credited in {StrongCandidateCastAppearancesMin} casts —"
          + " fetching it.");
        var candidate = await TryIngestCandidateAsync(candidateId, token);
        charactersFetched++;
        if (candidate is null)
        {
          continue;
        }

        if (await graphStore.PathExistsAsync(seedAComicVineId, seedBComicVineId, token))
        {
          return new CrawlResult(true, charactersFetched, issuesFetched);
        }

        EscalationEnqueueIssues(pendingIssueIds, queuedIssueIds, candidate);
      }
    }

    if (castsExamined >= EscalationIssueCastsMax)
    {
      Trace($"Escalation stopped at its {EscalationIssueCastsMax}-cast ceiling with budget still left.");
    }

    return new CrawlResult(false, charactersFetched, issuesFetched);
  }

  /// <summary>ADR-0014 step 5's recursion: a strong candidate's own issues become the
  ///   next round's candidate pool.</summary>
  private void EscalationEnqueueIssues(Queue<int> pendingIssueIds, HashSet<int> queuedIssueIds,
    ComicVineCharacter candidate)
  {
    Trace($"Escalating Enqueuing issues for {candidate.Name} (id {candidate.Id})");

    foreach (var issueId in candidate.IssueCredits.Select(i => i.Id))
    {
      if (queuedIssueIds.Add(issueId))
      {
        pendingIssueIds.Enqueue(issueId);
      }
    }
  }

  private Side ChooseSideToExpand()
  {
    if (_frontierA.Count == 0)
    {
      return Side.B;
    }

    if (_frontierB.Count == 0)
    {
      return Side.A;
    }

    return _frontierA.Count <= _frontierB.Count ? Side.A : Side.B;
  }

  private int? DequeueNextUnvisited(Side side)
  {
    var frontier = side == Side.A ? _frontierA : _frontierB;
    while (frontier.Count > 0)
    {
      var candidateId = frontier.Dequeue();
      if (!_visited.Contains(candidateId))
      {
        return candidateId;
      }
    }

    return null;
  }

  private void EnqueueFrontier(Queue<int> frontier, ComicVineCharacter character)
  {
    Trace($"Enqueuing {character.Name} (id {character.Id})");

    foreach (var id in character.CharacterFriends.Select(f => f.Id)
      .Concat(character.CharacterEnemies.Select(e => e.Id)))
    {
      if (!frontier.Contains(id))
      {
        frontier.Enqueue(id);
      }
    }
  }

  /// <summary>
  ///   IngestCharacterAsync for a crawl candidate, where one bad Comic Vine record shouldn't
  ///   sink the whole crawl: any failure but cancellation is traced, the id is marked visited
  ///   so no other route re-fetches it, and null comes back so the caller moves on. Seeds and
  ///   user-picked characters call IngestCharacterAsync directly — for them, failing loudly
  ///   is right.
  /// </summary>
  private async Task<ComicVineCharacter?> TryIngestCandidateAsync(int comicVineId, CancellationToken token)
  {
    try
    {
      return await IngestCharacterAsync(comicVineId, token);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      _visited.Add(comicVineId);
      Trace($"  Skipping {comicVineId}: Comic Vine fetch failed ({ex.Message}).");
      return null;
    }
  }

  /// <summary>
  ///   Ensures a character is fully persisted (Character node + issue_credits) and
  ///   checked for overlaps against the whole graph (ADR-0012). Public because it's also
  ///   useful standalone — e.g. seeding a single character picked from a Comic Vine search
  ///   that isn't in our graph yet at all.
  /// </summary>
  public async Task<ComicVineCharacter> IngestCharacterAsync(int comicVineId, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();
    // Logged before the fetch, not after: when Comic Vine sends something unparseable,
    // this is the only line that says which character it was.
    Trace($"Fetching character {comicVineId} from Comic Vine");
    var character = await characterSource.GetCharacterAsync(comicVineId, token);
    _visited.Add(comicVineId);

    Trace($"Ingesting {character.Name} (id {comicVineId})");

    var domainCharacter = character.ToDomain() with { IngestionDateTime = _timeProvider.GetUtcNow().UtcDateTime };
    var isNewCharacter = await graphStore.GetCharacterAsync(comicVineId) is null;
    await graphStore.UpsertCharacterAsync(domainCharacter);
    if (isNewCharacter)
    {
      CharacterAdded?.Invoke(domainCharacter);
    }

    await graphStore.UpsertCreditedInAsync(comicVineId,
      [.. character.IssueCredits.Select(i => new Issue(i.Id, i.Name, siteDetailUrl: i.SiteDetailUrl))]);

    _discoveredIssueIds.UnionWith(character.IssueCredits.Select(i => i.Id));

    Trace($"Ingested {character.Name} ({comicVineId}){(isNewCharacter ? " [new]" : " [refreshed]")}:"
      + $" {character.IssueCredits.Count} issue credit(s), {character.CharacterFriends.Count} friend(s),"
      + $" {character.CharacterEnemies.Count} enemy(ies).");

    return character;
  }

  private enum Side
  {
    A,
    B
  }

  public event Action<Character>? CharacterAdded;

  private readonly HashSet<int> _shownViaRandom = [];

  public async Task<Character?> PickRandomCharacterAsync(int? excludeCharacterId)
  {
    Trace("Selecting random character");

    var excludeIds = new HashSet<int>(_shownViaRandom);
    if (excludeCharacterId is int id) excludeIds.Add(id);

    var picked = await PopulatePicked(excludeIds);

    if (picked is null && _shownViaRandom.Count > 0)
    {
      // Everyone's been shown — start the rotation over.
      _shownViaRandom.Clear();
      var retryExcludeIds = excludeCharacterId is int retryId ? new HashSet<int> { retryId } : new HashSet<int>();
      picked = await PopulatePicked(retryExcludeIds);
    }

    if (picked is not null)
    {
      _shownViaRandom.Add(picked.ComicVineId);
    }

    // Flipped once per click, never per lookup — a reset's retry must stay in the same mode.
    _pickOldest = !_pickOldest;
    return picked;

    Task<Character?> PopulatePicked(HashSet<int> excludedIds) =>
      _pickOldest
        ? graphStore.GetLeastRecentlyIngestedCharacterAsync(excludedIds)
        : graphStore.GetRandomCharacterAsync(excludedIds);
  }

  public async Task RefreshIfStaleAsync(int comicVineId, CancellationToken token)
  {
    var character = await graphStore.GetCharacterAsync(comicVineId);

    if (character is null)
    {
      Trace($"RefreshIfStaleAsync: character {comicVineId} not found in graph. Ingesting...");
      await IngestCharacterAsync(comicVineId, token);
      return;
    }

    if (character.IngestionDateTime is null)
    {
      Trace($"RefreshIfStaleAsync: character {comicVineId} has no IngestionDateTime. Ingesting...");
      await IngestCharacterAsync(comicVineId, token);
      return;
    }

    var age = _timeProvider.GetUtcNow().UtcDateTime - character.IngestionDateTime.Value;
    if (age > TimeSpan.FromHours(1))
    {
      Trace($"Refreshing stale character {character.Name} (id {comicVineId}), last ingested {age.TotalHours:F1} hours ago.");
      await IngestCharacterAsync(comicVineId, token);
    }
  }
}
