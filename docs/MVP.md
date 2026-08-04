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
- No *automatic* live/on-demand crawling on a cache-miss (ADR-0005) — a
  user-initiated version (a "Try to find a connection" button, ticket 11)
  was added post-MVP; only automatic background crawling remains out of
  scope.

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
   for crawl *decisions*. Post-MVP (ticket 10), the crawl does fetch
   `/issue/{id}/` once per issue whose name is blank/"TPB", purely to
   enrich the stored name with the issue's Volume (series) name — a
   display concern, not a pathfinding one, and failures there degrade
   gracefully rather than blocking the crawl.

5. **`Ingest` CLI wiring** (done) — `--seed-id <comicVineId>` (repeatable,
   exactly 2) + `--budget <n>` (max new characters to ingest this run —
   see ADR-0010), reading `COMIC_VINE_API_KEY`/Neo4j connection from env.
   Ties 3 and 4 together into a runnable console app. Name-based lookup
   (`--seed <name>`, needing Comic Vine's own search API) is a deferred
   follow-up — IDs are known upfront for MVP's target query.

6. **Character search service** (done) —
   `IGraphStore.SearchCharactersAsync(query, limit)` in
   `OracleOfBatman.Graph`: case-insensitive substring match against
   Character names, alphabetical, bounded by `limit` (default 20). Lets
   the UI resolve typed text to a character id. Not yet called from
   `OracleOfBatman.Web` (Blazor Server has no separate API tier to expose
   this over HTTP as its own endpoint — see ADR-0009) — that's ticket 8.

7. **Path service** (done, see ADR-0011 for the full detail) —
   `IGraphStore.FindShortestPathAsync(characterAId, characterBId, maxDepth)`
   in `OracleOfBatman.Graph` (moved out of `Ingest` so `Web` doesn't have
   to depend on a console ingestion tool). Bounded BFS over whatever's
   cached in Neo4j, own max-depth bound independent of the crawl's (6, for
   now — not a considered decision, just a starting default). Returns a
   `Path?` — `Path(Characters, Hops)`, where each `Hop` carries one
   representative Connection (the pair's existing strongest-tier/earliest-
   date default) normalized to walk order; `null` covers every "not enough
   data yet" case (unseeded character, or no path within `maxDepth`)
   undifferentiated. `BatmanNumber` is `Hops.Count`, computed rather than
   stored. Called in-process from `OracleOfBatman.Web`'s search page
   (ticket 8).

8. **Web UI: search page** (done) — two `MudAutocomplete` character inputs
   (backed by ticket 6's search service), a Go button, calls the path
   service, renders the path as a plain list of Characters plus the Batman
   Number, or a "not enough data yet" alert. No filters, no node-diagram
   visualization — that's the full vision in `docs/UI.md`, deliberately
   deferred past MVP. Verified live in-browser against the real Aura
   instance: Soft Serve → Beast → Bloodscream, Batman Number 2.

9. **End-to-end smoke test** (done) —
   `dotnet run --project src/OracleOfBatman.Ingest -- --seed-id 176719 --seed-id 15734 --budget 50`,
   then confirm the web app finds the Soft Serve → Beast → Bloodscream
   path. Verified: searching both characters in the web UI and clicking Go
   renders "Batman Number: 2, Soft Serve → Beast → Bloodscream" against
   the real Aura instance.

## Post-MVP additions

These were built after the tickets above, in response to using the app for
real rather than being tickets planned up front.

10. **Richer Connections and human-readable hops** (done) — `Character` and
    `Connection`/`Hop` gained `ImageUrl`/`SiteDetailUrl` fields, populated
    from Comic Vine's `image`/`site_detail_url` and (for issues) fetched
    lazily per displayed hop (`IComicVineIssueSource`). The search page
    renders each hop as "{Character} was {tier phrase} in {issue} with
    {Character}", with `MudAvatar` thumbnails and `MudLink`s out to Comic
    Vine. When an issue's own name is blank or the generic "TPB" (common
    for collected editions), the crawl fetches the issue once, enriches the
    name with its Volume (series) name — `"{Volume}: {Issue}"`, or just the
    Volume alone if the issue name was blank — and caches it per crawl run
    so a heavily-shared issue is only fetched once. Failures fetching that
    Volume name (network blip, Comic Vine rate limiting) degrade to the
    original blank/TPB name rather than aborting the ingest.

11. **Comic Vine search + on-demand crawl from the UI** (done) —
    `IComicVineCharacterSearchSource` wraps `/search/?resources=character`,
    surfaced on the search page as "Can't find a character above? Search
    Comic Vine directly" with avatar'd results and "Use as A"/"Use as B"
    buttons (`ConnectionCrawler.IngestCharacterAsync`, public specifically
    for this single-character case). When two selected Characters aren't
    connected yet, a "Try to find a connection" button runs
    `PopulateConnectionsAsync` on demand from the UI rather than requiring
    a separate `Ingest` CLI run. Both actions show `MudSnackbar` progress/
    success/error feedback and are wrapped in try/catch — an unhandled
    exception kills the entire Blazor Server circuit (the whole session,
    not just the one action), which a rate-limit failure during a
    prolific character's ingest hit in practice (see ticket 10's caching/
    failure handling).

12. **Duplicate-relationship fix** (done) — `UpsertConnectionAsync`'s
    `MERGE` was keyed on a directional pattern
    (`(source)-[:CONNECTION]->(target)`), so the same real-world Same
    Issue connection, written with source/target swapped across different
    crawl runs, created a second relationship instead of matching the
    first. Fixed by canonicalizing Source/Target order (by
    `comic_vine_id`) before the `MERGE`, but only for tiers CONTEXT.md
    marks Symmetric — In-Universe Mention and Meta Mention are Directional,
    where source/target order is meaningful data, not an artifact
    (`InteractionTierExtensions.IsSymmetric()`). A one-time cleanup pass
    against the real Aura instance removed 5,983 pre-existing duplicate
    relationships (out of 19,423 total) caused by this bug.
