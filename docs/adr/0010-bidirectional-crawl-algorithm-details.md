---
status: accepted
---

# Bidirectional friend/enemy-BFS crawl, budget-bounded by new-character count

ADR-0005 named the ingest crawl an "expanding bidirectional crawl" but left
its actual mechanics vague ("expand outward... checking for overlap at each
layer, bounded by the request/depth budget"). This resolves the specifics,
elaborating ADR-0005 and MVP.md ticket 4 rather than superseding them.

**Algorithm**, given two seed Characters:

0. Free pre-check: query Neo4j for an existing path between the seeds
   *before* spending any Comic Vine request. Skip the crawl entirely if
   already connected.
1. Fetch both seeds' character records — Comic Vine's `/character/{id}/`
   returns `issue_credits`, `character_friends`, and `character_enemies`
   in one response, confirmed from real sample data (`jim-hammond.xml`,
   `jeff-the-land-shark.xml`) — so this is 2 requests total, not more.
2. Direct issue overlap between the seeds → Connection(s), done.
3. Direct friend/enemy overlap between the seeds → fetch that shared
   character, check their issues against both seeds' issues, create
   Connection(s) for any hits.
4. Otherwise, bidirectional BFS: each round, expand whichever side
   currently has the smaller frontier (classic meet-in-the-middle
   optimization — minimizes total requests against Comic Vine's 200/hour
   limit, see ADR-0004) by fetching one new not-yet-seen friend/enemy.
   Every newly-fetched character's issues are checked against **every
   character discovered so far on either side, not just the two original
   seeds** — this is what makes it capable of finding paths longer than
   2 hops, not just a direct-common-friend lookup.
5. Stop when the seeds are connected in the accumulated graph, or the
   budget runs out.

**Budget is a single number: max new characters ingested this run.** Since
each new character costs exactly one API request (step 1's single-response
fact above), request-budget and character-budget are the same count here —
no separate depth parameter needed for MVP; depth falls out naturally from
exhausting the budget or the frontiers meeting.

**`/characters` (plural, list endpoint) is not usable for this**: confirmed
against Comic Vine's own API documentation that `filter` only ANDs across
*different* fields (`filter=name:Batman,publisher:DC`), not ORs multiple
values of the same field — there's no way to fetch an arbitrary specific set
of character IDs in one call. The list endpoint also omits
`character_friends`/`character_enemies`/`issue_credits` entirely; only the
singular `/character/{id}/` endpoint has them. One-request-per-character
stands.

**`published_at` is deliberately left null** on crawl-created Connections —
fetching full issue detail (the only way to get `cover_date`) would cost an
extra request per shared issue found, and nothing downstream needs it yet:
MVP only ever produces one Interaction Tier (`SharedScene`) and one
Confidence (`Unverified`), so ADR-0007's "strongest tier, earliest date
wins" tie-break has nothing to actually tie-break on yet. Revisit once
something (the tie-break, or UI) needs it — see `docs/POST_MVP.md` for the
related image-URL/issue-link idea, deferred for the same reason (not needed
for this first pass).
