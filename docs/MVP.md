# MVP

Target query: **Soft Serve → Bloodscream** (Comic Vine ids 176719 and 15734).
Both Marvel/same-publisher, deliberately obscure — confirmed via a real
crawl run to be genuinely 2 hops apart (Soft Serve → Beast → Bloodscream),
a real test of multi-hop pathfinding, not a trivial one-degree case. An
earlier candidate pair, Jim Hammond and Jeff the Land Shark, turned out to
already share two issues directly once actually crawled — too trivial to
exercise the multi-hop path.

Scope cuts from the full design (see [CONTEXT.md](../CONTEXT.md) and
[docs/adr/](./adr/) for the full vision):

- No Mantle/Portrayal/Universe modeling — Character maps 1:1 to a Comic Vine
  entry. That machinery exists to collapse a Character across continuities;
  with one data source there's no second continuity to collapse yet.
- No multi-tier Interaction Tiers — every co-appearance is one `Same Issue`
  Connection (see ADR-0011), never promoted to a more specific tier. Comic
  Vine can't distinguish Direct Interaction from a Meta Mention anyway; the
  tier/strongest-wins logic has nothing to do until there's manually
  curated richer data.
- No live/on-demand crawling — only characters seeded via `ingest` are
  queryable (see ADR-0005).

## Tickets, in dependency order

0. **Acceptance scenario** — the Soft Serve / Bloodscream scenario,
   written before any of the code that makes it pass (outside-in), as a
   plain xUnit/NUnit test using `CONTEXT.md`'s vocabulary (see ADR-0009 —
   the outer BDD tool ADR-0006 specified doesn't carry over to .NET as-is;
   choosing a replacement is deferred). Expected to fail via
   `NotImplementedException` until tickets 1–7 are done — that's the point,
   not a bug. Each remaining ticket should turn one or more stubs into real
   implementations.

1. **Domain types** (`OracleOfBatman.Domain`) — `Character` (id, name,
   comic_vine_id), `Connection` (two character ids, at most one
   comic_issue_id, an Interaction Tier, a Confidence — MVP only ever
   produces `Unverified`, see ADR-0007).

2. **Neo4j schema** (see ADR-0007) —
   `(:Character {comic_vine_id, name})-[:CONNECTION {comic_issue_id, tier, confidence, published_at}]->(:Character)`.
   Multiple relationships between the same two Characters are expected and
   normal (Batman/Joker could have hundreds) — Neo4j natively supports
   this; no aggregation at write time. Writes must `MERGE` keyed on
   (character pair, comic_issue_id), not `CREATE` — the crawl rediscovers
   the same character/issue from both seed directions, and duplicates
   would silently break both BFS and the "one Connection per issue" rule.

3. **Comic Vine API client** — `character → { issue_credits,
   character_friends, character_enemies }`, all from the single
   `/character/{id}/` response (confirmed from real sample data — no
   separate request per field). The crawl (ticket 4) never fetches
   `/issue/{id}/` at all; see ADR-0010 for why. Needs rate-limit handling
   (200 requests/resource/hour) and in-run caching so the crawl never
   re-fetches the same character twice.

4. **Expanding crawl algorithm** (`OracleOfBatman.Ingest`, see ADR-0007 and
   ADR-0010 for the full detail) — for two seed Characters:
   1. Free pre-check: is there already a path between the seeds in Neo4j?
      If so, skip the crawl entirely.
   2. Fetch both seeds' character records (2 requests). Any issue in both
      seeds' `issue_credits` is a Same Issue Connection candidate (one
      `Unverified` Connection per shared issue — see ADR-0011).
   3. If none, compare the seeds' `character_friends`/`character_enemies`
      for overlap — free, already in hand. Fetch any shared character and
      check their issues against both seeds'.
   4. If still not connected, bidirectional BFS: each round, fetch one new
      not-yet-seen friend/enemy from whichever side has the smaller
      frontier (1 request = 1 budget unit — see ADR-0010 for why request-
      budget and character-budget are the same number here). Check every
      newly-fetched character's issues against **everyone discovered so
      far on either side**, not just the two seeds, so the crawl can find
      paths longer than 2 hops.
   Iterative, not recursive; writes discovered Characters/Connections into
   Neo4j as it goes, stopping when the seeds are connected in the
   accumulated graph or the character budget runs out. `published_at` is
   left null on these Connections (see ADR-0010) — no `/issue/{id}/` fetch
   during the crawl.

5. **`Ingest` CLI wiring** (done) — `--seed-id <comicVineId>` (repeatable,
   exactly 2) + `--budget <n>` (max new characters to ingest this run —
   see ADR-0010), reading `COMIC_VINE_API_KEY`/Neo4j connection from env.
   Ties 3 and 4 together into a runnable console app. Name-based lookup
   (`--seed <name>`, needing Comic Vine's own search API) is a deferred
   follow-up — IDs are known upfront for MVP's target query.

6. **Character search service** — substring match against what's in Neo4j,
   lets the UI resolve typed text to a character id. Called in-process from
   `OracleOfBatman.Web` (Blazor Server has no separate API tier to expose
   this over HTTP as its own endpoint — see ADR-0009).

7. **Path service** (see ADR-0011 for the full detail) —
   `IGraphStore.FindShortestPathAsync(characterAId, characterBId, maxDepth)`
   in the new `OracleOfBatman.Graph` project (moved out of `Ingest` so
   `Web` doesn't have to depend on a console ingestion tool). Bounded BFS
   over whatever's cached in Neo4j, own max-depth bound independent of the
   crawl's. Returns a `Path?` — `Path(Characters, Hops)`, where each `Hop`
   carries one representative Connection (the pair's existing
   strongest-tier/earliest-date default) normalized to walk order; `null`
   covers every "not enough data yet" case (unseeded character, or no path
   within `maxDepth`) undifferentiated. `BatmanNumber` is `Hops.Count`,
   computed rather than stored. Also in-process, called directly from
   `OracleOfBatman.Web`.

8. **Web UI: search page** — two autocomplete character inputs (backed by
   ticket 6's search service), submit, call the path service, render the
   path as a plain list or the "not enough data" state. No filters, no
   node-diagram visualization — that's the full vision in `docs/UI.md`,
   deliberately deferred past MVP.

9. **End-to-end smoke test** —
   `dotnet run --project src/OracleOfBatman.Ingest -- --seed-id 176719 --seed-id 15734 --budget 50`,
   then confirm the web app finds the Soft Serve → Beast → Bloodscream path.
