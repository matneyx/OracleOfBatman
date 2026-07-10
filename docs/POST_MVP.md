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

## Data sources

- Multi-source ingestion beyond Comic Vine: SuperHero API, RapidAPI DC
  Comics collection, `thatfiredev/dc-villains-api` — supplementary once
  Comic Vine's own coverage isn't enough.
- Non-comics media per ADR-0002's long-term scope: novels, webcomics,
  film, TV.

## Ingestion

- Live, on-demand crawling on an API cache-miss (ADR-0005) — deferred
  because it needs a background job/polling pattern rather than a
  blocking request, and risks exhausting Comic Vine's rate limit if a
  few concurrent cold queries hit at once.

## Frontend

- Cucumber/BDD (Cucumber.js) once there's real UI behavior to specify
  (ADR-0006) — the MVP frontend is one smoke-test button, nothing worth
  writing a scenario against yet.
- Full UI vision — search filters (Interaction Tier/Confidence/Canonicity
  floors, Shared Identity toggle, Universe pinning), the node-diagram
  results view, admin-triggered ingest button, and speculative
  community/merge-tooling features — see `docs/UI.md`. MVP ships a plain
  list rendering with no filters.

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
