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

- **Build ADR-0014's issue-cast escalation step (Team deferred)** — next
  up, scoped down: `PopulateConnectionsAsync` still only does ADR-0010's
  basic friends/enemies BFS; `Issue.character_credits` (just wired up) is
  captured but nothing consumes it yet. When the BFS exhausts its budget
  with no connection found, escalate to fetching full `/issue/{id}/` casts
  for the frontier's issues and check `character_credits` for strong
  candidates (2+ appearances, ADR-0014). Refinement over ADR-0014's
  original rule, decided but not yet built: if nobody hits the 2+
  threshold, fall back to weaker (1+) candidates rather than giving up
  outright. Team-side escalation (team rosters, `MEMBER_OF`) explicitly
  deferred — Team isn't a node yet at all (see below).
- **Model Team as a first-class Domain type/Neo4j node** (ADR-0014,
  designed not yet built) — same `MEMBER_OF` treatment `CREDITED_IN` got
  in ADR-0016: a Character's team memberships become real edges, used by
  the crawl's frontier-expansion/strong-candidate discovery (ADR-0014),
  never a Connection/Path segment itself. The Issue-as-node half of this
  original proposal is done (ADR-0016); Team is the remaining piece.
- **Detect provably-unreachable Character pairs** — e.g. every one of a
  Character's friends/enemies/teams/issue-credits already known to live in
  a completely un-crossed-over universe/imprint, so no amount of budget
  will ever find a path. Distinct from the existing best-effort cutoff
  (ADR-0014's escalation ladder gives up once its budget runs out, not
  because it proved impossibility) — calling this out with confidence
  needs some notion of a "universe"/imprint boundary in the data to reason
  about, which isn't modeled at all today (see `CONTEXT.md`'s Universe
  concept, unused until the MVP's single-publisher scope expands).
- **"Include Creators" option** — an opt-in that lets a Path also connect
  through shared writers/artists, not just co-credited Characters. Direct
  answer to the bullet above: two Characters can be genuinely
  un-crossed-over at the fictional-universe level yet still connect in
  the real world through a creator who worked on both — this is also
  plausibly how the Snoopy → Snoop Dogg query (the "Open questions"
  section below) actually resolves, if Mad Magazine's creator overlap
  turns out to matter more than its parody content does. Comic Vine's
  issue response already includes `person_credits`, so the raw data is
  there for free (same pattern as `character_friends`/`teams`). Bigger
  design question than it looks: is a shared creator a Same-Issue-strength
  Connection, or a distinct, weaker tier (someone can write two wildly
  different Characters without them ever "meeting")? Likely ties into the
  deferred multi-tier Interaction Tier system and Minimum Canonicity
  control (ADR-0008, `docs/UI.md`) rather than being a bolt-on toggle.
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
- Nightly refresh job — rate-limited, bulk version of the manual "Random
  Character" button (ADR-0016): re-ingest already-known Characters
  oldest-`ingestion_date_time`-first, re-fetching current friends/enemies/
  issue credits and unconditionally re-`MERGE`ing `CREDITED_IN` edges to
  catch new credits gained since last checked. No new Neo4j schema needed
  — `IngestCharacterAsync` already does exactly this per-Character, just a
  scheduler calling it in bulk. Scales linearly against Comic Vine's
  200/hour limit (one request per Character refreshed), so past a couple
  hundred Characters a single nightly pass no longer fits in one hour and
  refreshes would need to spread across a rolling window rather than
  refreshing everyone every night. Not scheduled — needs a job-scheduling
  decision (this project has no scheduler yet) before it can be built.

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
