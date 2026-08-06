---
status: accepted
---

# Escalating multi-signal crawl: friends/enemies/teams BFS, then issue/team-cast bridge discovery

Design-only ADR, like ADR-0013 — not yet implemented. Extends ADR-0010's
crawl algorithm rather than replacing it: the free pre-check and direct
seed-to-seed overlap checks (steps 0-2 below) are unchanged. What's new is
an explicit depth boundary on the friends/enemies BFS phase, and a further
escalation tier below it that ADR-0010 only gestured at (POST_MVP.md's
"issue-cast-based bridge discovery" idea) — now designed in full, and
extended to teams as well as issues.

## Motivation

Comic Vine already has all the data needed to answer most connection
queries — we just don't have direct DB access, only a rate-limited API
(200 requests/resource/hour, ADR-0004). The whole crawl design is about
intelligently rationing that budget across which "layer" of Comic Vine's
actual graph to pull in next, not about a storage or compute limitation.
ADR-0010's original ladder used only `character_friends`/`character_enemies`
for frontier expansion and had no defined point at which to stop that phase
and try something else. Comic Vine's data has more free/cheap signals going
unused:
- `/character/{id}/` already returns `teams` (a character's own team
  memberships) in the same response as `issue_credits`/friends/enemies —
  free, no extra request, exactly the pattern ADR-0010 already exploits.
- `/team/{id}/` returns a full roster (`characters`) plus
  `character_friends`/`character_enemies` for the team itself — one
  request reveals many candidate characters at once.
- `/issue/{id}/` returns the full `character_credits` cast (everyone
  credited, not just the two characters being compared) and `team_credits`
  — both unused today since the crawl currently never fetches full issue
  detail at all (ADR-0010).

## The escalation ladder

0. Free pre-check: already connected in Neo4j? Skip the crawl entirely
   (unchanged from ADR-0010).
1. Direct `issue_credits` overlap between the two seeds (unchanged).
2. Direct friend/enemy overlap between the seeds (unchanged).
3. **Bidirectional BFS via friends, enemies, *and* teams together** —
   teammates (from a character's free `teams` list) are now an equally-
   weighted neighbor alongside friends/enemies, not a separate phase.
   Expand whichever frontier is smaller each round (unchanged
   smaller-frontier-first optimization); check every newly-fetched
   character's `issue_credits` against everyone discovered so far on
   either side (unchanged). New: bounded by a **new, distinct depth
   limit** — a starting default of 10, not a fully considered value any
   more than ADR-0010's own `maxDepth` default of 6 was — separate from
   both `FindShortestPathAsync`'s own `maxDepth` (bounds the path *query*)
   and the crawl's overall character-fetch budget (bounds total requests
   this run). Three independent numbers answering three different
   questions: how deep to search before escalating, how many characters
   total we're willing to fetch, and how far we'll ever display/traverse
   for a Batman Number.
4. **If that depth limit is exhausted with no connection found, escalate**:
   for the outermost (frontier) characters reached, fetch full detail —
   `/issue/{id}/` for their issues, `/team/{id}/` for their teams — to get
   each one's *complete* cast (`character_credits` / `characters`), not
   just the friends/enemies/teammates already known. This is the
   previously-deferred "issue-cast-based bridge discovery" idea
   (POST_MVP.md), now paired with the equivalent team-roster discovery,
   and given a concrete trigger: only after the cheaper BFS phase fails,
   not unconditionally.
5. **Don't individually fetch every name from those casts/rosters** — a
   single issue or team can have a large cast, and importing all of them
   defeats the point of rationing the request budget. Only a character
   appearing in **2 or more** of the newly-pulled casts/rosters (issue
   casts and team rosters pooled into one shared count, not two separate
   thresholds) counts as a "strong candidate" worth the cost of fetching
   individually. This can recurse — a strong candidate's own issues/teams
   become the next round's candidate pool — until the overall budget runs
   out.

## Request-budget economics: one unified counter

A `/team/{id}/` or `/issue/{id}/` cast-fetch costs exactly one budget unit,
the same as fetching one character via `/character/{id}/`, even though it
can reveal many candidate IDs at once (none of which are actually
*ingested* — their own issue_credits/friends/enemies fetched — until
individually pulled down later, each costing their own unit). This is a
deliberate asymmetry: one budget unit can mean "one character fully
ingested" *or* "many un-ingested candidate IDs discovered," depending on
what kind of request it was. Kept as a single counter anyway (rather than
separate character-budget/team-budget/issue-budget lines) for the same
reason ADR-0010 originally collapsed request-budget and character-budget
into one number: simplicity, no second parameter to reason about.

## `teams`/`team_credits`: discovery only, no Tier weighting, no pathfinding participation

Three related decisions, all the same shape as choices already made
elsewhere in this project:

- **Not a stronger Interaction Tier.** Two Characters both on a team's
  active roster in a given issue isn't confirmation they shared a scene
  any more than bare Same Issue co-occurrence is — Comic Vine's data
  can't distinguish tier strength at all (MVP.md's existing scope cut).
  `team_credits` only ever informs *discovery* (who's worth fetching
  next), never what Tier gets recorded.
- **Team is a first-class node**, for the identical reason `Issue` became
  one in ADR-0013: many Characters sharing one Team is the same O(N²)
  edge-blowup shape as many Characters sharing one Issue, and persisting
  it once means "these two Characters were both on the X-Men" becomes a
  free structural fact for every future query, not something
  recomputed/refetched each time.
  ```
  (:Character)-[:MEMBER_OF]->(:Team {comic_vine_id, name, site_detail_url})
  ```
  No properties on `MEMBER_OF` — same reasoning as `CREDITED_IN` (ADR-0013):
  nothing to store per membership.
- **`MEMBER_OF` is never traversed by `FindShortestPathAsync`.** Only
  shared `CREDITED_IN`/Issue membership is a real connection for Batman
  Number purposes. Two Characters who are both on the X-Men, with no
  confirmed shared issue between them, are still "not enough data yet" —
  team co-membership is purely a signal for the *crawl* to decide who to
  fetch next, never a path segment itself. Consequence for
  implementation: `FindShortestPathAsync`'s Cypher must match on
  `:CREDITED_IN` specifically, not any wildcard relationship type that
  would accidentally let `MEMBER_OF` hops count toward a path.

## Relationship to ADR-0013

The escalation step's "ingest Issues"/"ingest Teams" language means the
same thing ADR-0013 already defined: `MERGE` an `Issue`/`Team` node and
the crediting/membership edge from each discovered Character. No new
storage decision beyond extending the same pattern to a second node type.
Unlike `Issue`, `Team` has no existing live data to migrate — nothing
today persists team membership at all, so this is pure net-new structure,
not a migration.

## Deferred / not decided here

- The exact depth-limit default (10) and character-budget default (today:
  50, `ConnectBudget` in `Home.razor`) are starting points, revisit once
  there's real usage data — same spirit as ADR-0010's original `maxDepth`
  default.
- Whether the "strong candidate" threshold (2+) should scale with cast/
  roster size for very large ensembles (e.g. a 50-person team roster vs.
  a 3-person one) — not raised during design, default to a flat constant
  until real data suggests otherwise.
