# Post-MVP

Ideas and known gaps explicitly deferred during design — not scheduled, not
committed. When one of these actually gets picked up, turn it into real
tickets (see `docs/MVP.md` for the format), then remove it from here.

## Domain model

- Full Mantle/Portrayal/Universe identity resolution (default mantle-holder
  heuristic + manual override) — designed in `CONTEXT.md` but unused until
  Character data actually spans multiple Universes/mantle-holders, which
  the MVP's single-publisher scope doesn't exercise.
- Full multi-tier Interaction Tier system (Direct Interaction / Shared
  Scene / In-Universe Mention / Meta Mention / Shared Identity,
  strongest-tier-wins) — the MVP treats every Connection as `Unverified`
  (see ADR-0007) since Comic Vine's API can't distinguish tiers on its own.
- **Verification/curation UI** — let a user browse a Character pair's
  `Unverified` Connections (one per shared issue, per ADR-0007) and either
  confirm the real Interaction Tier (making it `Verified`) or reject it as
  not a real interaction. This is what actually populates the full 5-tier
  system; MVP's crawl only ever produces `Unverified` same-issue records.
  Watch for false positives from idiomatic/exclamatory language if this
  ever does automated (or human-assisted-search) Mention detection —
  "Jesus Christ!" as an exclamation isn't a reference to the character
  Jesus Christ. Distinct from both Namesake Cloud (multiple *characters*
  sharing a name) and Shared Identity (same character, independent
  adaptations) — a fourth, separate failure mode: a character's name
  colliding with ordinary idiomatic language.
- Canonicity (Official/Unofficial/Parody, ADR-0008) and the Title concept
  it's cascaded from are designed in `CONTEXT.md` but unused by MVP, same
  as the rest of the multi-tier system above.
- Collapse same-Character Portrayals into a single node instead of linking
  them via the Shared Identity tier (see `CONTEXT.md`) — noted as a
  possible future refinement when Shared Identity was added, not decided.
- **Manually exclude a Character or Issue from ever being used as a path
  segment** — e.g. Mad Magazine parodies, artist/creator biography issues,
  "Wizard"-style meta/making-of content, or any other collection whose
  cast is a real Comic Vine credit but not a genuine in-story interaction.
  Distinct from the Verification/curation UI bullet above (which
  confirms/rejects one specific *Connection*): this is a blanket, standing
  exclusion of a whole node from pathfinding entirely, likely a boolean
  flag checked by the BFS neighbor lookup. Related to the Minimum
  Canonicity control (`docs/UI.md`, ADR-0008) and the Snoopy → Snoop Dogg
  / Mad Magazine open question below, but a coarser, manual override rather
  than an automatic Canonicity tier.

## Data sources

- Multi-source ingestion beyond Comic Vine: SuperHero API, RapidAPI DC
  Comics collection, `thatfiredev/dc-villains-api` — supplementary once
  Comic Vine's own coverage isn't enough.
- Non-comics media per ADR-0002's long-term scope: novels, webcomics,
  film, TV.

## Ingestion

- **Model Issue (and Team) as first-class Domain types/Neo4j nodes**
  (ADR-0013/0014/0015, designed not yet built) — replaces pairwise
  per-issue `CONNECTION` edges (O(N²) for an N-Character issue) with
  mirrored array properties (`Character.issue_credits` /
  `Issue.character_credits`, ADR-0015 — supersedes ADR-0013's original
  `CREDITED_IN` edge proposal), making a Same Issue connection a
  structural fact rather than a written record, lazily materialized only
  once two already-ingested Characters are confirmed to share an Issue.
  `Team` gets the same treatment for the crawl's own discovery use (never
  a Connection/Path segment itself). Includes a simplified live-data
  migration (existing `CONNECTION` data can just be dropped — Character
  data and its `issue_credits` arrays are fine as the fresh starting
  point), a unified Issue-enrichment fetch (image/Volume/name together,
  triggered on render), and an escalating friends/enemies/teams-BFS-then-
  issue/team-cast-discovery crawl algorithm. Pathfinding moves from a
  single Cypher `shortestPath()` query to an application-level BFS, since
  arrays aren't traversable edges — an accepted tradeoff for storing less
  data. Not scheduled — a real chunk of work across the crawl, the
  writer, and the path query, deliberately deferred rather than rushed
  alongside other changes.
- Live, *automatic* on-demand crawling on an API cache-miss (ADR-0005) —
  still deferred because it needs a background job/polling pattern rather
  than a blocking request, and risks exhausting Comic Vine's rate limit if
  a few concurrent cold queries hit at once. A user-*initiated* version
  ("Try to find a connection" button on the search page, a blocking
  request the user explicitly opts into) is done instead — lower risk
  since it's one request at a time, not automatic.
- "Try to find a shorter path" admin action — deliberately re-crawl an
  already-connected pair anyway, ignoring the "stop once connected" rule
  (ADR-0010), specifically hunting for a shorter path (ADR-0012's accepted
  partial-graph limitation). Not scheduled.
- **Proposed: revert to real `Character-[:CREDITED_IN]->Issue` edges,
  created eagerly at ingest time, superseding ADR-0015's array/lazy-
  materialization/app-level-BFS mechanism** — under active discussion, not
  decided (a grilling session was started and interrupted; resume it
  before building any of this). Motivating problem: two popular
  Characters with huge `issue_credits` arrays (e.g. Batman, Deadpool) take
  a very long time to connect under Slice 5's app-level, multi-round-trip
  BFS. Argument for reverting: ADR-0015 chose arrays over edges for two
  reasons — avoiding wasted Comic Vine calls on issues nobody ends up
  caring about, and storing less data by deferring node creation. The
  first reason no longer holds now that Slice 6 fully decoupled Issue
  *enrichment* (the Comic Vine fetch) from Issue *node existence* —
  enrichment is already lazy and render-triggered regardless of whether
  the bare node was created eagerly or lazily, so eager edges wouldn't
  reintroduce the API-call cost arrays were partly chosen to avoid. The
  second reason (raw storage volume) still applies and would need to be
  explicitly accepted. Open questions identified but not yet resolved:
  - Does lazy materialization go away entirely, or coexist with eager
    edges somehow?
  - Does this make the Connection-edge fast-path work from this same
    session (caching successful Path hops as pairwise Character-Character
    `:CONNECTION` edges, checked before falling back to array BFS —
    RED tests exist, GREEN not yet written) redundant? Real
    `CREDITED_IN` edges would let Neo4j's native `shortestPath()` do the
    traversal directly, which may remove the need for a separate
    Character-Character cache layer entirely.
  - What happens to `Character.issue_credits`/`Issue.character_credits`,
    `FindOverlappingIssuesAsync`, and the rest of the Slices 2-5 surface —
    removed, or kept for something?
  - Does existing local graph data (already in the array shape) need a
    migration pass, or does it get dropped and rebuilt like ADR-0015's own
    migration did?
  - Given the project's convention of writing an ADR for every major
    storage/pathfinding decision (0007 through 0015), this should get its
    own ADR (0016) once decided, the same way ADR-0015 itself came out of
    a grilling session.
- **Ingestion/usage tracking** — also under discussion, not decided:
  - `ingestion_date` on both `Character` and `Issue` nodes, updated every
    time a Character gets (re-)ingested. Issues likely get it set once
    (creation or first enrichment — not yet decided which) and never
    refreshed, since issues aren't expected to be re-ingested the way a
    Character can be (see `PersistCharacterAsync`/`IngestCharacterAsync`'s
    always-refetch behavior from this same session).
  - Three usage-frequency counters: how often a Character is used as one
    of the two outermost/seed characters in a search, how often a
    Character is used as an intermediate/connecting (bridging) character
    in a found Path, and how often an Issue is used as a hop in a found
    Path. Not yet decided where these increment (every successful
    `FindShortestPathAsync`? only fresh crawls?) or what drives the need
    for them beyond the Random Character idea below.
  - A "Random Character" button that isn't actually random — it picks
    whichever Character has the oldest `ingestion_date`, i.e. a disguised
    "refresh the stalest data" mechanism. Depends entirely on
    `ingestion_date` existing first.
- Nightly refresh job — rate-limited, re-fetch every already-known
  Character's current `issue_credits` (they may have gained new issue
  credits since last checked), update the stored list, and re-run
  `FindOverlappingIssuesAsync` (ADR-0012) against the whole graph to catch
  connections that didn't exist yet at ingestion time. Builds directly on
  ADR-0012's persisted `issue_credits` + overlap-query primitives — no new
  Neo4j schema needed, just a scheduler. Scales linearly against Comic
  Vine's 200/hour limit (one request per Character refreshed), so past a
  couple hundred Characters a single nightly pass no longer fits in one
  hour and refreshes would need to spread across a rolling window (e.g.
  oldest-checked-first) rather than refreshing everyone every night. Not
  scheduled — needs a job-scheduling decision (this project has no
  scheduler yet) before it can be built.

## Frontend

- Cucumber/BDD (Cucumber.js) once there's real UI behavior to specify
  (ADR-0006) — the MVP frontend is one smoke-test button, nothing worth
  writing a scenario against yet.
- Full UI vision — search filters (Interaction Tier/Confidence/Canonicity
  floors, Shared Identity toggle, Universe pinning), the node-diagram
  results view, admin-triggered ingest button, and speculative
  community/merge-tooling features — see `docs/UI.md`. MVP ships a plain
  list rendering with no filters.
- Clearer in-progress feedback while a path search is actually running —
  today "Try to find a connection" only swaps the button's own text
  (`_connecting`); the plain "Go" search (`FindPathAsync`) has no loading
  state at all. Worth a more visible affordance (spinner/progress bar)
  once the per-character/per-issue Snackbar events give a sense of what
  "in progress" should actually look like.

## Engineering discipline

- Promote `clippy::too_many_lines` / `clippy::unwrap_used` from `warn` to
  `deny` once there's enough real code for them to mean something
  (`docs/STYLE.md`).
- CI-level `.editorconfig` enforcement via `editorconfig-checker`, if the
  editor-hint-only version proves insufficient.

## Open questions

- **The Snoopy → Snoop Dogg query** — this is literally the query that
  inspired the whole project, and it may only be answerable through Mad
  Magazine, which lampooned both. Resolved via the Minimum Canonicity
  control (see `docs/UI.md` and ADR-0008): defaults to Unofficial (so the
  Blondie-strip case counts by default), with Parody as an explicit,
  stricter-than-default opt-*in* rather than opt-out.

## Namesake Cloud (name tentative)

- A "browse everyone who shares a name" feature — e.g. Martha Kent, Martha
  Wayne, and Martha "Em" Cypress (*Revival*) aren't the same Character (no
  shared origin, no claimed identity) and aren't a Mantle (no succession)
  — they just coincidentally share a first name. This is a different
  relationship entirely from the Connection/Interaction Tier model: a
  name-based grouping/browse facet, not a graph edge. Don't confuse it
  with Shared Identity (same canonical Character, independent Portrayals)
  or Mantle (same role, different Characters, connected narrative) — all
  three look similar at a glance but are genuinely different relationships.
  Not yet formalized into `CONTEXT.md` since it's unbuilt and unnamed for
  real; "Namesake Cloud" is a placeholder.

## Operations

- Actually pick and deploy to a hosting target — deliberately left
  undecided so far; config already lives in env vars, not
  provider-specific files, so this should mostly be a config exercise
  when it happens.
