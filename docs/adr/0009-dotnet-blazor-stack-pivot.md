---
status: accepted
---

# Pivot backend/frontend to .NET Blazor + MudBlazor; Docker scoped to Neo4j only

This is a "just for fun / working with AI" side project, explicitly time-boxed
by the author's day job — not a project to learn a new language deeply. The
Rust (Axum/neo4rs) backend and React/HeroUI frontend were both unfamiliar
stacks being learned simultaneously, which fought the "don't spend a lot of
time on this" constraint. Almost no code existed yet (~80 lines of Rust,
default Vite template on the frontend), so the swap cost is a scaffold
rewrite, not a rewrite of working features.

**New stack**: a single .NET solution —
- `src/OracleOfBatman.Domain` — shared types (Character, Connection,
  Interaction Tier, ...), same role as the old `crates/domain`.
- `src/OracleOfBatman.Web` — Blazor Web App (Server interactivity) +
  MudBlazor, replacing both the old `crates/api` (Axum) and `frontend/`
  (React/HeroUI/Vite). Server-render mode means UI code can call Neo4j
  directly in-process — no separate HTTP API tier is needed for a
  single-app hobby project.
- `src/OracleOfBatman.Ingest` — console app, same role as the old
  `crates/ingest` (Comic Vine crawl → Neo4j).
- `Neo4j.Driver` (official .NET driver) replaces `neo4rs`.

**Neo4j is unchanged** — still the graph DB, still Docker-hosted locally.
Everything else that used to run in Docker (API, frontend, ingest) now runs
via `dotnet run`/`dotnet watch` directly on the host; `docker-compose.yml` is
scoped down to the `neo4j` service only, and `docker-compose.override.yml`
(which existed solely for Rust/npm hot-reload-in-container) is deleted.

This doesn't touch ADR-0001/0002's core scope decision (publisher- and
medium-agnostic graph) or ADR-0004/0005/0007/0008 (Comic Vine source, crawl
strategy, Connection schema, Canonicity) — those are all data-model/ingestion
decisions independent of implementation language.

**Supersedes, in part**:
- ADR-0003 (Tiger-Style, repo-wide) — the *discipline* is kept, but its
  Rust/TS-specific enforcement mechanisms (`clippy`, `rustfmt`, oxlint,
  `tiger-assert`) no longer apply. `docs/STYLE.md` is updated in place per
  its own "living document" clause rather than superseded wholesale — see
  that file for the current C#/Blazor-specific rules.
- ADR-0006 (Cucumber/BDD, Rust-only) — the `cucumber` crate doesn't carry
  over; Reqnroll is the closest .NET equivalent, but adopting an outer BDD
  tool is deliberately deferred rather than decided here, to avoid spending
  more of the time budget re-deciding tooling than building the MVP. Until
  then, `docs/MVP.md`'s ticket 0 acceptance scenario is a plain xUnit/NUnit
  test instead of a `.feature` file.

Trade-offs accepted going in:
- Losing the Rust-learning side-goal this project originally carried
  (dropped deliberately, not a casualty).
- Blazor Server keeps UI and data-access in one process/one language, which
  is simpler for a solo hobby project, but means the "frontend" has no
  independent existence if a native mobile client or separate SPA is ever
  wanted later — an acceptable bridge to cross later, not now.
