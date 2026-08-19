---
status: accepted
---

# Real `CREDITED_IN` edges (eager, per-ingest); friends/enemies as discovery arrays; ingestion/usage tracking; a disguised-refresh Random Character button

Design-only ADR, like the ones it supersedes parts of — not yet
implemented. Reached by grilling, the same way ADR-0015 itself was.
Supersedes ADR-0015's storage mechanism and pathfinding approach
specifically; ADR-0013's original `CREDITED_IN` edge proposal is
effectively revived, with one addition ADR-0013 didn't have (see Issue
`character_credits`, below). ADR-0014's escalation ladder is unchanged in
its algorithm shape — friends/enemies/teams BFS, then issue/team-cast
bridge discovery, strong-candidate threshold — only its trigger point and
underlying storage move (see Relationship to ADR-0014).

## Motivation

Two popular Characters with large `issue_credits` arrays (e.g. Batman,
Deadpool) take a very long time to connect under ADR-0015 Slice 5's
application-level, multi-round-trip BFS — a real, observed problem, not a
hypothetical one. ADR-0015 chose arrays over edges for two reasons:

1. Avoiding wasted Comic Vine calls on issues nobody ends up caring about.
2. Storing less data by deferring Issue-node creation until a confirmed
   overlap between two already-ingested Characters.

Reason 1 no longer holds: ADR-0015 Slice 6 already decoupled Issue
*enrichment* (the Comic Vine fetch for image/volume/name/cast) from Issue
*node existence*. Enrichment was already lazy and render-triggered
regardless of whether the bare node was created eagerly or lazily, so
eager edges don't reintroduce the API-call cost arrays were partly chosen
to avoid — a bare `Issue` stub node costs nothing to create; only
enrichment costs a request, and that stays exactly as lazy as it already
was.

Reason 2 (raw storage volume) still applies and is explicitly accepted
here as the tradeoff: every credited Issue gets a node the moment its
crediting Character is ingested, not just the ones two Characters happen
to share.

## Decision: `CREDITED_IN` edges, eager, created at ingest time

```
(:Character)-[:CREDITED_IN]->(:Issue)
```

Whenever a Character is ingested (both `PersistCharacterAsync` and the
crawl's `IngestCharacterAsync`), for every entry in that Character's own
`issue_credits` response (id + name — already in hand, zero extra Comic
Vine request), `MERGE` an `Issue` stub node and a `CREDITED_IN` edge to it.
No properties on the edge — same reasoning as ADR-0013: nothing to store
per credit.

`Character.issue_credits` is removed entirely. "What is this Character
credited in" is now answered by traversing the edge, not reading an array.

## `Issue.character_credits` survives, but changes meaning

Unlike ADR-0013 (which had no equivalent), `Issue.character_credits` is
kept — but it's no longer "which already-ingested Characters share this
Issue" (redundant with edges now that those exist). It becomes the *raw
Comic Vine cast list*: every character id Comic Vine reports as credited
on this issue, whether or not that Character has ever been ingested here.
That can't be edges — there's no Character node to point at for an
uningested credit — so it has to stay an array.

Populated lazily, at **enrichment** time (`/issue/{id}/` fetch), not at
stub-creation: a stub only has an id and a name gleaned for free from
someone else's ingest response — we haven't actually looked at the Issue
itself yet, so stamping any real data (including this array) at
stub-creation would be premature. This is the same free-data mechanism
ADR-0014 already described (materialized Issue/Team `character_credits`/
roster arrays accumulating as a side effect of unrelated ingestions) — it
just no longer requires the two-Character overlap-confirmation step
ADR-0015 gated it behind. Every Issue that exists as a node at all can
accumulate this array the first time it's enriched, unconditionally.

## `character_friends`/`character_enemies`: raw-id arrays, not edges

New `Character` properties `friend_ids`/`enemy_ids`, populated at ingest
time from the same `/character/{id}/` response (free, no extra request).
Deliberately **not** edges (`FRIEND_OF`/`ENEMY_OF`), unlike the
`CREDITED_IN` decision above — a friend or enemy is very often not
ingested yet, and creating a real `Character` node just to have somewhere
to point an edge is riskier than an Issue stub: a bare-stub Character node
(id only, no name/image) could leak into search results or character
listings before it's ever properly ingested, whereas a bare Issue stub
only ever surfaces inside a confirmed Path hop. Arrays avoid that risk
entirely.

Same treatment as `Team`'s `MEMBER_OF` (ADR-0014): discovery-only, used
purely by the crawl's frontier-expansion step to decide who to fetch next,
never a path segment, never traversed by `FindShortestPathAsync`, never
displayed.

## Pathfinding: native `shortestPath()`, application-level BFS removed

`FindShortestPathAsync`/`PathExistsAsync` return to a single Cypher
`shortestPath()` query over `CREDITED_IN*` (ADR-0013's original Bacon-
Number-style approach), since `CREDITED_IN` is a real, traversable edge
again. ADR-0015 Slice 5's multi-round-trip application-level BFS is
deleted, not kept as a fallback — there's nothing left for it to be a
fallback *from*.

## `Connection`/`UpsertConnectionAsync` and the Connection-edge fast path: deleted outright

Both ADR-0013 and ADR-0015 kept `Connection`/`UpsertConnectionAsync`
around for a future non-issue Interaction Tier system (Direct Interaction,
Shared Scene, etc. — the deferred 5-tier system in `POST_MVP.md`). That
reasoning is retired here: if/when that system gets built, it more
naturally lives as `Tier`/`Confidence` properties directly on the
`CREDITED_IN` edge itself (Same Issue already *is* that edge; a human
verifying a pair as a stronger tier is refining an edge that already
exists, not creating a parallel one). Resurrecting a whole second edge
type for that purpose would just be more surface area than the eventual
feature needs.

This also retires the just-started Connection-edge fast-path cache work
(this session's tasks #39/#40 — RED tests written, never made GREEN):
checking a cached `Connection` before falling back to array-BFS bought
nothing once there's no array-BFS left to be faster than. `Connection`,
`UpsertConnectionAsync`, and every RED test written against that cache
behavior are removed, not left dormant.

`FindOverlappingIssuesAsync` and `UpsertCharacterIssueCreditsAsync` are
also removed — there's no more overlap-confirmation step; every credit
becomes an edge unconditionally at ingest.

## `ingestion_date`: Character vs. Issue timing differ

- **Character**: stamped on every real ingest — both `PersistCharacterAsync`
  and `IngestCharacterAsync` set it to "now" whenever they write fresh
  Comic Vine data, the same way `Connection.DateFirstConfirmed` already
  works today (the caller supplies the value; the graph store just
  persists whatever's on the record — no clock logic inside the store
  itself, keeps it testable).
- **Issue**: stamped at **enrichment** time, not stub-creation — matching
  the same reasoning as `character_credits` above. A stub Issue node
  hasn't actually been "ingested" in any meaningful sense; only once
  `/issue/{id}/` is actually fetched has real data about the Issue been
  pulled in.

## Three usage-frequency counters

Motivation: curiosity, and surfacing "popular" characters/issues in a
future UI (a leaderboard or similar) — not feeding any crawl heuristic
today.

- **`seed_use_count`** (Character) — bumped every time a Character is
  selected as Character A or B for a search **attempt**, regardless of
  whether a path is found. Chosen deliberately asymmetric from the other
  two: being *picked* is itself the interesting event for a future "most
  popular characters" surface, independent of outcome.
- **`bridge_use_count`** (Character) — bumped only when a Character
  appears as an intermediate (non-endpoint) node in a **successfully
  found** Path.
- **`path_use_count`** (Issue) — bumped only when an Issue appears as a
  hop in a **successfully found** Path.

All three increment on every successful `FindShortestPathAsync` call that
qualifies, including a repeat lookup of an already-known path (now cheap,
native `shortestPath()`) — these are query-frequency counters, not
discovery-frequency counters. No flag distinguishing "fresh crawl" from
"already connected" needs to be threaded through call sites.

## Random Character: a disguised least-recently-ingested picker

A button that looks like "pick a random Character" but actually returns
whichever Character has the oldest `ingestion_date` — a "refresh the
stalest data" mechanism in disguise. Depends entirely on `ingestion_date`
existing.

- Available on **both** the Character A and Character B slots.
- **Excludes** whichever Character is currently selected in the other
  slot, so Random can never hand back a trivial A==B pick.
- Selecting Random does **not** itself trigger a re-ingest — re-ingest of
  the seeds still only happens on Go, to keep the button snappy. This
  means the "oldest ingestion_date" pointer doesn't naturally rotate on
  its own between clicks (it would have, for free, if selection itself
  refreshed the timestamp — rejected specifically to avoid a Comic Vine
  round-trip on every Random click).
- Because of that, an explicit exclusion mechanism is needed: an
  in-memory, **circuit-scoped** set of previously-shown-via-Random ids,
  living alongside `ConnectionCrawler`'s existing per-circuit state (which
  already persists for the life of a Blazor Server circuit today). Reset
  once it covers every Character (all exhausted → start over), or
  implicitly on a fresh circuit/page refresh. Deliberately not persisted
  beyond a session — this is UX polish ("don't show the same Character
  twice in a row") not a fairness guarantee; a refresh forgiving everyone
  shown so far is a harmless cosmetic quirk, not a bug.

## Feedback: `CharacterAdded` stays, `IssueConnectionConfirmed` moves to Go-click time

`CharacterAdded` (fired when a genuinely new Character node is created)
is unchanged — a new Character is still a real, ingestion-time event in
this model.

`IssueConnectionConfirmed` (ADR-0015-era, this session's task #39) is
removed as an ingestion-time event — it was built around the lazy-
materialization "confirmed overlap" moment, which no longer exists (every
credit becomes an edge unconditionally, with no comparison against other
Characters happening at ingest time). The only place a "you're connected!"
moment still exists is when `FindShortestPathAsync` returns a Path at
Go-click time. Snackbar feedback moves there instead: one Snackbar per hop
in the successfully-found Path, fired from `Home.razor` at the same point
Issues get enriched for display — i.e. **Issues are enriched (and their
Snackbar fired) when they're queued up for display while building Hops
and the Path**, not during ingestion.

## Relationship to ADR-0014

ADR-0014's escalation ladder (bidirectional friends/enemies/teams BFS,
then issue/team-cast bridge discovery, 2+-appearances strong-candidate
rule) is unchanged in shape. What moves:

- **Trigger point**: connections are now made at Go-click time (running
  the escalation ladder against the two selected Characters), not
  proactively at individual-Character-ingest time. Ingesting a Character
  via Comic Vine search (`PersistCharacterAsync`) no longer does any
  overlap/crawl work at all — it just persists the Character and its
  `CREDITED_IN`/`friend_ids`/`enemy_ids` data.
- **Underlying storage**: "check already-materialized `character_credits`
  before paying for a fresh fetch" (ADR-0014's refinement) still applies,
  now against the always-eager Issue stub nodes rather than lazily-
  materialized ones — a stub always exists once any Character citing it
  has been ingested; only its enrichment (and thus its `character_credits`
  cast list) may still be missing.
- **Best-effort cutoff messaging**: if the escalation ladder exhausts its
  budget with no path found, the user sees a message acknowledging the
  search prioritizes UX over exhaustive search and that trying again may
  reveal more connections (new data won't have appeared, but the
  escalation's own randomness/ordering could turn up a path a previous
  attempt's budget cut off before reaching) — not designed in further
  detail here; exact wording and retry semantics are a `Home.razor`-level
  concern for whoever builds the Go-click flow.

## Migration

Drop and rebuild, same call as ADR-0015's own migration: delete every
`issue_credits` property and every materialized `Issue` node, keep bare
`Character` nodes. Real ingestion reconstructs `CREDITED_IN` edges, Issue
stubs, and `friend_ids`/`enemy_ids` arrays from scratch the next time each
Character is used. No in-place migration script — current local data is
low-volume and cheap to rebuild, same reasoning as before.

## `IGraphStore` shape (sketch, not final code)

- `UpsertCharacterAsync` — now also writes `friend_ids`, `enemy_ids`,
  `ingestion_date` (caller-supplied), and increments `seed_use_count` when
  called as part of a seed selection specifically (exact call-site
  responsibility TBD during implementation).
- `UpsertCharacterIssueCreditsAsync` — removed. Replaced by whatever
  `IngestCharacterAsync`/`PersistCharacterAsync` do to `MERGE`
  `CREDITED_IN` edges + Issue stubs directly (likely folded into
  `UpsertCharacterAsync` itself, or a new dedicated method — TBD).
- `FindOverlappingIssuesAsync` — removed.
- `Connection`/`UpsertConnectionAsync` — removed.
- `UpsertIssueAsync` — gains the `character_credits` raw-cast-array
  write-back and `ingestion_date` stamp, alongside the existing
  image/volume/name/site-link enrichment write (ADR-0015 Slice 6,
  unchanged mechanism).
- `PathExistsAsync`/`FindShortestPathAsync` — same signatures, Cypher
  rewritten for `CREDITED_IN*` `shortestPath()` traversal.
- New counter-increment methods or properties for `bridge_use_count`
  (Character) and `path_use_count` (Issue) — exact shape TBD during
  implementation (likely folded into the same write that persists a
  newly-found Path, rather than a separate round trip per Character/Issue).

## Deferred / not decided here

- Exact mechanics of the Go-click escalation flow itself (block-ingestion
  sizing, the specific best-effort budget/cutoff value, retry-message
  wording) — this ADR settles storage and tracking; the crawl-trigger
  relocation's fine detail is implementation work for whoever builds it,
  same spirit as ADR-0014's own deferred depth-limit tuning.
- Where exactly `seed_use_count` increments gets called from (every
  `FindPathAsync` attempt in `Home.razor`, or somewhere inside
  `ConnectionCrawler`) — an implementation detail, not a design fork.
