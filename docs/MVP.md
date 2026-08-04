# MVP

Target query: **Jim Hammond (the original android Human Torch) → Jeff the
Shark**. Both Marvel/same-publisher, deliberately obscure and several hops
apart — a real test of multi-hop pathfinding, not a trivial one-degree case.

Scope cuts from the full design (see [CONTEXT.md](../CONTEXT.md) and
[docs/adr/](./adr/) for the full vision):

- No Mantle/Portrayal/Universe modeling — Character maps 1:1 to a Comic Vine
  entry. That machinery exists to collapse a Character across continuities;
  with one data source there's no second continuity to collapse yet.
- No multi-tier Interaction Tiers — every co-appearance is one undifferentiated
  `Connection` edge. Comic Vine can't distinguish Direct Interaction from a
  Meta Mention anyway; the tier/strongest-wins logic has nothing to do until
  there's manually curated richer data.
- No live/on-demand crawling — only characters seeded via `ingest` are
  queryable (see ADR-0005).

## Tickets, in dependency order

0. **Acceptance scenario** — the Jim Hammond / Jeff the Shark scenario,
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

3. **Comic Vine API client** — character → issues, character →
   `character_friends`/`character_enemies` (free on the same response,
   no extra request), issue → `character_credits`. Needs rate-limit
   handling (200 requests/resource/hour) and in-run caching so the crawl
   never re-fetches the same issue/character twice.

4. **Expanding crawl algorithm** (`OracleOfBatman.Ingest`, see ADR-0007) — for two
   seed Characters:
   1. Fetch each seed's own issue list; any issue in both is a same-issue
      Connection candidate (one `Unverified` Connection per shared issue).
   2. If none, compare each seed's `character_friends`/`character_enemies`
      for overlap — cheap, already in hand from step 1's fetch.
   3. If still none, expand outward bidirectionally: fetch friends'/
      enemies' own issues and friends/enemies, checking for overlap at
      each layer, bounded by the request/depth budget (Tiger Style's
      fixed-loop-bound rule).
   4. When examining a candidate shared issue, also pull its
      `character_credits` (full cast) — surfaces extra bridge candidates
      without extra per-character requests.
   Iterative, not recursive; writes discovered Characters/Connections into
   Neo4j as it goes, stopping when the frontiers meet or the budget runs
   out.

5. **`Ingest` CLI wiring** — `--seed <name>` (repeatable) + a budget flag,
   reading `COMIC_VINE_API_KEY`/Neo4j connection from env. Ties 3 and 4
   together into a runnable console app.

6. **Character search service** — substring match against what's in Neo4j,
   lets the UI resolve typed text to a character id. Called in-process from
   `OracleOfBatman.Web` (Blazor Server has no separate API tier to expose
   this over HTTP as its own endpoint — see ADR-0009).

7. **Path service** — bounded BFS over whatever's cached in Neo4j, returns
   the path + Batman Number, or a "not enough data yet" result for an
   unseeded pair. Own max-depth bound, independent of the crawl's. Also
   in-process, called directly from `OracleOfBatman.Web`.

8. **Web UI: search page** — two autocomplete character inputs (backed by
   ticket 6's search service), submit, call the path service, render the
   path as a plain list or the "not enough data" state. No filters, no
   node-diagram visualization — that's the full vision in `docs/UI.md`,
   deliberately deferred past MVP.

9. **End-to-end smoke test** —
   `dotnet run --project src/OracleOfBatman.Ingest -- --seed "Jim Hammond" --seed "Jeff the Shark"`,
   then confirm the web app finds the path.
