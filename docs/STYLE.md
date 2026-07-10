# Style Guide

This project follows an engineering discipline inspired by TigerBeetle's
[TIGER_STYLE.md](https://github.com/tigerbeetle/tigerbeetle/blob/main/docs/TIGER_STYLE.md),
adapted for a Rust/Tokio/Neo4j backend and a React/TypeScript frontend —
neither of which is the Zig/C, zero-dependency, statically-allocated,
synchronous-by-design world Tiger Style was written for.

**This is a living document.** When a rule turns out not to fit this stack,
we change the rule *here* rather than quietly ignoring it. Every deviation
below is deliberate and explained — if you hit a new one, add it, don't just
work around it silently.

## Naming

- Rust: `snake_case` for functions/variables/files (already idiomatic).
- TypeScript/React: kept language-idiomatic instead — `camelCase` for
  variables/functions, `PascalCase` for components/types. **Deviation**:
  forcing `snake_case` onto React/TS would fight the ecosystem (hooks,
  component conventions) for no real benefit.
- Both: suffix variable names with units/qualifiers, most-significant last
  (`latency_ms_max`, not `maxLatencyMs`). Prefer equal-length paired names
  (`source`/`target` over `src`/`dest`). Sort alphabetically when order is
  otherwise arbitrary. Prefix helper functions with their caller's name to
  show call history.

## Safety & assertions

- Rust: `assert!`/`debug_assert!` liberally — pre/post-conditions, both
  "positive space" (what must be true) and "negative space" (what must
  never happen). Pair assertions across a boundary (e.g. assert before a
  Neo4j write and again after reading it back). `clippy::unwrap_used` is a
  workspace-level warning: handle errors explicitly instead of silently
  panicking past them.
- Frontend: use [`tiger-assert`](https://github.com/eugeny-dementev/tiger-assert)
  the same way — assert invariants at trust boundaries (component props,
  parsed API responses), not for routine control flow.
- Explicitly-sized integers: kept for domain-level quantities (appearance
  counts, Interaction Tier ordinals → `u32`/`u64`). **Deviation**: `usize`
  stays for indexing/lengths/slice operations — Rust's standard library is
  natively `usize`-typed there, and fighting it with casts adds truncation
  risk for no safety gain. Be deliberate about width; don't let it be an
  accident.
- Fixed upper bound on all loops/queues — kept, and load-bearing: the
  Batman Number BFS must have an explicit max-depth bound, or a query
  between two loosely-connected Characters can walk the entire graph.
- No recursion, in either language — graph traversal is an explicit
  queue/stack, not language recursion.

## Control flow

- Push `if`s up, `for`s down; split compound conditions into nested
  `if`/`else`; centralize branching in one place and keep leaf functions
  non-branchy. Applies to both languages, including extracting branchy
  logic out of JSX into named functions/hooks.
- **Dropped/reinterpreted**: "functions run to completion without
  suspending" is incompatible with `async fn`/`.await` (Rust) and
  `async`/`await`/Promises (TS) by construction — suspension *is* the
  point. Reinterpreted as: an async function should represent one clear
  unit of async work with obvious suspension points, not suspend
  conditionally scattered through deep branching.

## Functions & file size

- Hard limit: 70 lines per function, both languages. Enforced by
  `clippy::too_many_lines` (threshold in `/clippy.toml`) and oxlint's
  `max-lines-per-function` (`frontend/.oxlintrc.json`).
- File-level: 300-line warning via oxlint's `max-lines`. This specific
  number is our own addition, not from the original guide (which only says
  "important things near the top").

## Memory & allocation

- **Dropped**: "statically allocate all memory at startup, none after" —
  impossible for a Tokio HTTP service or a browser SPA; both allocate
  per-request/per-render by construction, correctly so.
- Reinterpreted as: avoid *unnecessary* or *unbounded* allocation — don't
  clone large graph structures per-request when a reference/slice works,
  don't grow an unbounded cache, avoid allocation churn in hot loops where
  a preallocated buffer is easy. Declare variables at the smallest scope
  that works, in both languages.

## Dependencies

- **Dropped**: "zero dependencies except the toolchain" — already
  incompatible with the stack we chose (axum, tokio, neo4rs, serde,
  reqwest, React, HeroUI, Tailwind).
- Reinterpreted as: no *unnecessary* dependency creep. Before adding a new
  crate or npm package, check whether std or an existing dependency
  already covers it. Prefer widely-used, actively-maintained packages —
  supply-chain risk is still real even though zero-deps isn't achievable.

## Testing

- Kept in spirit: test valid *and* invalid inputs, write test names that
  state the goal and method, not just the function under test.
- **Out of scope**: TigerBeetle's deterministic simulation testing is a
  large, distinctive investment that doesn't fit a hobby project at this
  stage. Explicitly scoped down, not an oversight.
- **Addition, not from the original guide**: Cucumber/BDD as an outer
  acceptance-testing loop on top of the assertion-heavy unit tests above —
  see ADR-0006. `.feature` files are written in `CONTEXT.md`'s vocabulary
  (Character, Connection, Batman Number, ...), not implementation terms;
  if a scenario needs a word that isn't in `CONTEXT.md`, that's a sign the
  glossary is missing something, not that the scenario should use jargon.
  Rust-only for now — added to the frontend once there's real UI behavior
  to specify. System-level scenarios spanning more than one crate live in
  `crates/e2e`, against a real ephemeral Neo4j (`testcontainers-modules`),
  not a mock.

## Performance

- Kept as a design-time habit: think about Neo4j query cost, network
  round-trips, and batching *before* writing the query, not after it's
  slow. No numeric back-of-envelope budget exists yet — add one once
  ingestion/query patterns are real.

## Formatting

- Rust: `rustfmt` (`/rustfmt.toml` — 100 columns, 4-space indent), checked
  via `cargo fmt --check`.
- TypeScript/frontend: 100-column intent kept, but **2-space indent
  kept instead of 4** — the dominant React/TS ecosystem convention, and
  fighting it via formatter config is friction with no real payoff.
- Comments explain *why*, not *what* — matches this project's general
  convention already (see the root instructions this repo follows).
- `.editorconfig` covers indent width/style, line endings, trailing
  whitespace, and final newline for file types `rustfmt`/oxlint don't
  format (YAML, TOML, Markdown, Dockerfiles). It's an editor hint, not an
  enforced check — most editors treat `max_line_length` as a visual ruler
  at best. `rustfmt`/oxlint remain the actual enforcement for `.rs`/`.ts`.
