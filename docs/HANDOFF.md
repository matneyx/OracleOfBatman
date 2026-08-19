# Handoff: Oracle of Batman

Written 2026-08-19 ahead of an Anthropic account migration (personal → Teams), which is expected to drop chat history and possibly local plugin/skill config. This doc is meant to let a fresh Claude Code instance (or a human) pick this project up cold.

## What this project is

A "six degrees of separation" site for fictional characters (like Oracle of Bacon, but characters instead of actors) — find the shortest connection path ("Batman Number") between any two characters across publishers and media.

- **Stack**: Neo4j (graph DB) + .NET Blazor Web App (Server interactivity) + MudBlazor. `Neo4j.Driver` used in-process from the Blazor app — no separate API tier. Docker is scoped to Neo4j only; the web app and ingest console app run via `dotnet run`/`dotnet watch`.
- **Working directory**: `C:\projects\personal\OracleOfBatman`
- **Solution layout**:
  - `src/OracleOfBatman.Domain` — shared domain types
  - `src/OracleOfBatman.Graph` — Neo4j access, Comic Vine ingestion/crawl logic
  - `src/OracleOfBatman.Web` — Blazor + MudBlazor UI
  - `src/OracleOfBatman.Ingest` — crawl console app
- **v1 data source**: Comic Vine API (Marvel's official API was discontinued). Free key, non-commercial only, rate-limited (200 req/resource/hour + velocity throttling). Other candidates flagged for later multi-publisher expansion: SuperHero API, RapidAPI DC Comics collection, thatfiredev/dc-villains-api.
- Original stack attempt was Rust (Axum/neo4rs) + React/HeroUI/Vite — abandoned early (~80 LOC) because juggling two unfamiliar stacks fought the project's "time-boxed side project" constraint (ADR-0009).

**Read `CONTEXT.md` first** — it's the domain-language glossary (Character/Universe/Portrayal/Mantle/Title/Issue/Team/Connection/Path/Hop/Interaction Tier/Confidence/Canonicity) and is the source of truth for terminology. Read `docs/adr/` in numeric order for the decision history; the most recent ones (0013–0016) supersede earlier storage decisions and are the current design.

## Current design state (as of ADR-0016)

The storage model has churned across three ADRs — know the arc so you don't reimplement something already superseded:

1. **ADR-0013**: proposed `CREDITED_IN` edges `(:Character)-[:CREDITED_IN]->(:Issue)`.
2. **ADR-0015**: replaced edges with mirrored arrays (`Character.issue_credits` / `Issue.character_credits`) to avoid eager Issue-node creation and wasted Comic Vine calls. Added application-level multi-round-trip BFS for pathfinding since there were no graph edges to traverse natively.
3. **ADR-0016 (current, accepted, *not yet implemented*)**: reverses course back to real eager `CREDITED_IN` edges, because the array approach's BFS was too slow for high-degree characters (Batman, Deadpool) and the original motivation for arrays (avoiding wasted Comic Vine calls) no longer held once Issue *enrichment* was already decoupled from Issue *node existence*. Key points:
   - `CREDITED_IN` edges created eagerly at ingest time (bare Issue stub nodes, cheap — no Comic Vine cost until enrichment).
   - `Character.issue_credits` removed entirely.
   - `Issue.character_credits` kept but repurposed: raw Comic Vine cast list, populated lazily at enrichment time.
   - `friend_ids`/`enemy_ids` become plain arrays on Character (not edges) — discovery-only signal for crawl frontier expansion, never a path segment.
   - Pathfinding reverts to a single Cypher `shortestPath()` query over `CREDITED_IN*` — the ADR-0015 application-level BFS is deleted outright, not kept as fallback.
   - `Connection`/`UpsertConnectionAsync` and the whole Connection-edge concept are **deleted outright** (not deferred) — a future Interaction Tier system would live as properties on `CREDITED_IN` edges instead.
   - New usage counters: `seed_use_count` (Character, search picks), `bridge_use_count` (Character, intermediate path hops), `path_use_count` (Issue, path hops) — curiosity/future-leaderboard only, don't feed crawl heuristics.
   - "Random Character" button is a disguised least-recently-ingested picker (oldest `ingestion_date`), not truly random.
   - Migration strategy is drop-and-rebuild (no in-place script) — data volume is low and cheap to re-crawl.

**Working tree at handoff time is mid-implementation of ADR-0016**: `Connection.cs` and its tests are already deleted, `Character.cs`/`Issue.cs`/`Hop.cs`/`ConnectionCrawler.cs`/`Neo4jGraphWriter.cs`/`IGraphStore.cs` are modified, and new contract-test infrastructure exists (`GraphStoreContractTests.cs`, `Neo4jGraphWriterContractTests.cs`, `Neo4jContainerFixture.cs`, `FakeGraphStoreContractTests.cs`) — this looks like a shared-contract test suite being introduced so `FakeGraphStore` and the real `Neo4jGraphWriter` get verified against the same behavioral contract. Run `git status` / `git diff --stat` to see exactly where it was left, and `dotnet test` to see what's red vs green before continuing.

## Working agreements (important — don't relitigate)

- **Strict TDD, and a specific division of labor**: write the failing test (RED) first, always. As of 2026-08-05, the user explicitly wants *the assistant* to write RED and do REFACTOR, and *the user* writes the implementation to go GREEN — deliberately, as a token/cost control after a session burned through both Comic Vine's rate limit and a lot of agent-driven-implementation tokens. Don't slip back into "I'll just implement this once" without the user raising it first.
- Test framework: xUnit.
- A "Tiger Style" (TigerBeetle-inspired) engineering discipline applies repo-wide, adapted per-language — see `docs/STYLE.md` and ADR-0003.
- Outer-loop BDD/Cucumber approach was adopted early (ADR-0006) but the current stack pivot (ADR-0009, .NET) may not have carried that outer-loop tool forward literally — check current test projects before assuming Cucumber/SpecFlow is in use; strict TDD unit tests are the confirmed inner loop regardless.
- Design decisions get made via a "grilling" style session (stress-testing an idea) before being written up as an ADR — ADR-0015 and ADR-0016 were both reached this way.

## Plugins/skills to reinstall on the new account

Local Claude Code plugin config lives in `C:\Users\dmatney\.claude\plugins\` (`installed_plugins.json`, `known_marketplaces.json`, `settings.json`'s `enabledPlugins`). This is local machine/profile state, not server-side chat history — it may or may not survive the account migration, but reinstalling is cheap if it doesn't. Marketplaces and plugins currently installed:

### Marketplaces
```bash
/plugin marketplace add https://github.com/juliusbrussee/caveman.git
/plugin marketplace add https://github.com/DrCatHicks/learning-opportunities.git
```
A third marketplace was known but not clearly used for enabled plugins — add only if you want to browse it:
```bash
/plugin marketplace add https://www.claudepluginhub.com/api/collections/c89hrl9cofirjXznOhS7wguVOB0Q16EO/mx-claude-plugin-hub/marketplace.json
```
(`claude-plugins-official` is the built-in Anthropic marketplace — no action needed, it's added by default.)

### Plugins to install
```bash
/plugin install caveman@caveman
/plugin install learning-opportunities@learning-opportunities
/plugin install learning-opportunities-auto@learning-opportunities
/plugin install orient@learning-opportunities
```

### What each one does
- **caveman** — always-on terse "caveman mode" response style (drops filler words, keeps all technical substance). This session was running it at `full` intensity. Also provides skills: `caveman-commit`, `caveman-review`, `caveman-stats`, `caveman-compress`, `caveman-help`, `caveman` (level switch), `cavecrew` (decision guide for delegating to caveman-style subagents), plus subagents `cavecrew-builder`, `cavecrew-investigator`, `cavecrew-reviewer`.
  - Outstanding TODO from this session: the caveman statusline badge was never wired up. To finish it, add to `C:\Users\dmatney\.claude\settings.json`:
    ```json
    "statusLine": {
      "type": "command",
      "command": "powershell -ExecutionPolicy Bypass -File \"C:\\Users\\dmatney\\.claude\\plugins\\cache\\caveman\\caveman\\<version-hash>\\src\\hooks\\caveman-statusline.ps1\""
    }
    ```
    (the `<version-hash>` path segment will differ after a fresh install — check `C:\Users\dmatney\.claude\plugins\cache\caveman\caveman\` for the actual installed folder name.)
- **learning-opportunities** (+ `-auto` + `orient`) — facilitates interactive learning exercises after architectural work (new files, schema changes, refactors), framed around treating design decisions as learning opportunities.

### Built-in skills used this session (no reinstall needed — ship with Claude Code)
`code-review`, `codebase-design`, `domain-modeling`, `diagnose`/`diagnosing-bugs`, `tdd`, `resolving-merge-conflicts`, `grilling`, `research`, `simplify`, `run`, `init`, `security-review`, and others — these are part of the base Claude Code skill set, not plugin-installed, so they should just be present again automatically.

## Where to look for more context
- `CONTEXT.md` — domain glossary, source of truth for terminology
- `docs/adr/0001` through `0016` — full decision history, read in order
- `docs/MVP.md` — MVP ticket list
- `docs/POST_MVP.md` — deferred features (curation UI, live-crawl-on-cache-miss, Interaction Tier verification, etc.)
- `docs/UI.md` — full UI vision (search screen, filters, node-diagram results — MVP itself ships a plain list)
- `docs/STYLE.md` — Tiger-Style engineering discipline adaptation
