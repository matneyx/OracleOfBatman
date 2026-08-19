---
status: accepted
---

# Model Issue as a first-class Neo4j node; Same Issue connections become structural

Design-only ADR — the decisions below are not yet implemented. A future
ticket builds them, matching how ADR-0010 designed the crawl algorithm
before MVP ticket 4 built it.

> **Superseded in part by ADR-0015, then ADR-0016.** The problem
> statement and "Same Issue becomes structural" below are still accurate.
> ADR-0015 briefly replaced `CREDITED_IN` edges with mirrored array
> properties; ADR-0016 reverts back to real, eager `CREDITED_IN` edges —
> effectively this ADR's original storage mechanism, restored — but drops
> the "keep Connection for future tiers" line below: ADR-0016 retires
> `Connection`/`UpsertConnectionAsync` entirely, on the reasoning that a
> future Interaction Tier system belongs on the `CREDITED_IN` edge itself
> rather than a parallel edge type. Read ADR-0016 for the current plan.

## Problem

Same Issue is currently stored as a pairwise `CONNECTION` relationship per
issue, per Character pair — `(source)-[:CONNECTION {comic_issue_id, tier,
confidence, comic_issue_name, comic_issue_site_detail_url}]->(target)`. For
an issue crediting N Characters, this is O(N²) relationships, each carrying
its own copy of that issue's name and link. Real numbers from this session:
ingesting Wolverine alone (heavily crossed-over with the existing 30-
Character graph) produced 525 `CONNECTION` relationships from one crawl
call. The same directional-`MERGE`-keyed-on-a-pair shape was also the root
cause of a real duplicate-relationship bug fixed earlier this session
(ADR fix, MVP.md ticket 12): the same real-world pair, discovered in
opposite Source/Target order across different crawl runs, created two
relationships instead of one.

`CONTEXT.md` already talks about Issue as if it were a real entity — "A
Connection belongs to at most one Title (via its Issue)" — and ADR-0008's
designed-but-unbuilt Canonicity system needs a Title flag to cascade down
through its Issues to every Connection derived from them, which is much
more natural against real Issue/Title nodes than per-relationship
denormalized properties.

Comic Vine itself can only ever tell us two Characters are credited on the
same issue — never that they actually interacted. This is deliberately the
same shape as Oracle of Bacon's own actor-credits model (not Crossover
Wiki's directional/weighted analysis): a credits list, not an interaction
graph.

## Decision: Issue as a node, CREDITED_IN edges, no properties

```
(:Character {comic_vine_id, name, image_url, site_detail_url})
    -[:CREDITED_IN]->
(:Issue {comic_vine_id, name, volume_name, site_detail_url})
```

`CREDITED_IN` carries no properties — there's nothing to store per credit,
since Tier/Confidence for this tier are now implicit from the pattern match
itself (see below), not a stored fact. Character's `issue_credits` array
property goes away entirely, replaced by real edges.

## Consequence: Same Issue connections become purely structural

"Two Characters share an Issue" *is* the Same Issue connection — nothing
gets explicitly detected or written for it anymore. This eliminates, for
the Same Issue path:
- `IGraphStore.FindOverlappingIssuesAsync` — removed entirely.
- The per-overlap `Connection` construction and `UpsertConnectionAsync`
  call in `ConnectionCrawler.IngestCharacterAsync` — removed entirely.

Ingesting a Character becomes: upsert the Character node, then `MERGE` one
`CREDITED_IN` edge (and its target `Issue` node) per issue credit. No
overlap computation step exists anymore — the moment those edges exist,
every other Character already credited on those Issues is automatically
two hops away.

**`Connection`/`UpsertConnectionAsync` are not deleted.** No code path
today produces `SharedIdentity`, `DirectInteraction`, `SharedScene`, or
`InUniverseMention`/`MetaMention` (confirmed by grep — only `SameIssue` is
ever written), but these are designed, not speculative: `CONTEXT.md` and
`docs/POST_MVP.md`'s curation UI plan to produce them later as direct
Character-Character edges (e.g. a human verifying a Same-Issue pair as a
real Direct Interaction). `IGraphStore` keeps `Connection`/
`UpsertConnectionAsync` for that day; Same Issue is simply the first tier
to stop needing them.

## Pathfinding: Bacon-Number style, no mixed-tier hop normalization

Since 100% of real data today is Same Issue (verified by grep), every
Character-to-Character path is an alternating
Character-Issue-Character-Issue-...-Character walk — the same shape as
Oracle of Bacon's actor-movie-actor pattern. `FindShortestPathAsync`'s
Cypher becomes `shortestPath((a:Character)-[:CREDITED_IN*..{{maxDepth*2}}]-(b:Character))`
(the schema only connects Character↔Issue, so alternation is automatic —
no filtering needed), and the Batman Number is the returned relationship
count divided by 2, exactly Oracle of Bacon's own convention. Hops are
built by walking `nodes(p)` in pairs of Characters separated by the
Issue node between them (index 0, 2, 4, ... are Characters; 1, 3, 5, ...
are the Issues connecting them).

**Explicitly not solved now**: what happens when a future non-issue tier
(a direct Character-Character edge) needs to combine with `CREDITED_IN`
hops in the same path — a mixed path would have inconsistent
relationship-count-per-hop (1 for a direct edge, 2 for an Issue-mediated
one), and `shortestPath()`'s raw relationship count wouldn't be a valid
Batman Number anymore. This is a deliberate YAGNI call, not an oversight:
building a general mixed-hop-aware traversal now means designing against
tiers with no ingestion code and no build date. Flagged here so it's found
deliberately, not rediscovered as a bug, whenever a non-issue tier
actually gets built.

## Issue metadata: raw properties, not a precomputed display string

`Issue` stores `name` (the issue's own name from Comic Vine — may be blank
or the generic `"TPB"`) and `volume_name` (the series/volume title) as
separate raw properties, not a precombined display string. Display
combining is a computed concern (Domain layer or render time), per the
existing rule (MVP.md ticket 10, unchanged by this ADR): if `Name` is
blank, show `VolumeName` alone; if `Name` is `"TPB"`, show
`"{VolumeName}: TPB"`; otherwise show `Name` alone (no Volume fetch even
attempted in that case — see below).

**The Volume-fetch trigger stays narrow** — only issues whose own name is
blank or `"TPB"` get a full `/issue/{id}/` fetch to learn their Volume;
well-named issues just use the name already in hand, same as today.
Considered widening this to fetch every issue's Volume unconditionally
(so `{Volume}: {Issue}` could be shown consistently everywhere), but
rejected: since `Issue` is now a real, shared node, this fetch already
amortizes to *once per distinct issue, ever* (not once per Connection, not
once per crawl run) — a real improvement over today — but a prolific
character's first ingest could still front-load a large one-time burst of
requests if every issue triggered it, the same shape of risk that tripped
Comic Vine's rate limit during this session's Wolverine test. The
resilience fix (graceful degradation on failure) means a burst failure no
longer crashes anything, but there's no need to invite a bigger burst than
necessary. Can be widened later if the plain-name-only display for
well-named issues feels inconsistent in practice.

## Migration

Existing live Aura data (as of this ADR: ~13,445 `CONNECTION` relationships,
all Same Issue, plus `issue_credits` arrays on every Character) migrates
in two passes:

1. **Rebuild `CREDITED_IN` edges from `issue_credits`** — the authoritative,
   complete source (every Character's own Comic Vine fetch), not the
   derived `CONNECTION` edges. For each Character, `UNWIND` its
   `issue_credits` array, `MERGE` an `Issue` node per id, `MERGE` a
   `CREDITED_IN` edge to it.
2. **Harvest `name`/`site_detail_url` from existing `CONNECTION` edges** —
   `issue_credits` is bare ids with no display metadata; that only ever
   lived on the old edges, denormalized per pair. Best-effort: any one
   `CONNECTION` edge referencing a given `comic_issue_id` supplies that
   `Issue` node's `name`/`site_detail_url` (they should all agree). Falls
   back to blank if no edge happens to have it — an inherited limitation
   from today (e.g. Beast's connections, never reached by the interrupted
   backfill script this session), not a new one introduced by migration.
   Rejected re-fetching fresh from Comic Vine during migration instead —
   correct but costs a request per distinct issue for no real benefit over
   reusing what's already been paid for.

Then delete every `CONNECTION` relationship and every `issue_credits`
array property — both fully superseded.

## IGraphStore shape (sketch, not final code)

- `UpsertCharacterIssueCreditsAsync(comicVineId, IReadOnlyList<IssueCredit> issueCredits)`
  replaces the current `IReadOnlyList<int>`-only version — `IssueCredit`
  carries `Id`/`Name`/`SiteDetailUrl` (already in hand from the character's
  own Comic Vine response), one Cypher call per Character (`UNWIND`),
  matching today's one-request/one-write-per-Character economy.
- `FindOverlappingIssuesAsync` — removed.
- `Connection`/`UpsertConnectionAsync` — unchanged, kept for future
  non-issue tiers.
- `PathExistsAsync`/`FindShortestPathAsync` — same signatures, Cypher
  rewritten for `CREDITED_IN` traversal as described above.
- New Domain type: `Issue(int ComicVineId, string? Name, string? VolumeName, string? SiteDetailUrl)`,
  with the blank/TPB/plain display-combining rule as a computed property or
  extension method (mirrors `InteractionTierExtensions`'s existing
  pattern).
