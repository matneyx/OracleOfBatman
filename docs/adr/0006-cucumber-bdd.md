---
status: accepted
---

# Adopt Cucumber/BDD, Rust-only for now, outside-in with TDD

We're adding Cucumber (the `cucumber` crate, Gherkin `.feature` files) as an
outer testing loop on top of the existing Tiger-Style assertion-heavy unit
tests (see ADR-0003). Scenarios are written in `CONTEXT.md`'s vocabulary
(Character, Connection, Batman Number, ...), reinforcing the same ubiquitous
language DDD already gives us there — a scenario like "a Character is
connected to another Character" should read the same whether a developer or
a domain expert is reading it.

Scope, for now: **Rust only**. The frontend has essentially no behavior yet
(one smoke-test button), so there's nothing for Cucumber.js to meaningfully
specify; add it there once real UI behavior exists.

System-level scenarios that span more than one crate (the MVP scenario needs
both `ingest`'s crawl and `api`'s path query) live in a new `crates/e2e`
crate rather than inside `api` or `ingest`'s own `tests/`, since Rust's test
harness is per-crate. They run against a real, ephemeral Neo4j via
`testcontainers-modules`' `neo4j` feature — not mocked, but isolated from the
dev/prod instance. `api` and `ingest` were split into thin `main.rs` +
`lib.rs` so `e2e` can call their logic in-process instead of spawning
subprocesses.

Outside-in relationship with TDD: Cucumber scenarios are the **acceptance/
outer loop** (does the system do the right observable thing), Tiger-Style
`#[test]`/`assert!` functions are the **inner loop** (is this function
correct). The MVP's first scenario (`crates/e2e/tests/features/batman_number.feature`)
was written before any of the code that makes it pass — that's deliberate:
it's the spec the MVP tickets in `docs/MVP.md` build toward, and it's
expected to fail (`todo!()` panics) until they're done.
