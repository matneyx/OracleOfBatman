# Style Guide

This project follows an engineering discipline inspired by TigerBeetle's
[TIGER_STYLE.md](https://github.com/tigerbeetle/tigerbeetle/blob/main/docs/TIGER_STYLE.md),
adapted for a .NET/Blazor/Neo4j stack (see [ADR-0009](./adr/0009-dotnet-blazor-stack-pivot.md)
for why it isn't Rust/React anymore) — a garbage-collected, exception-using,
browser-facing world quite different from the Zig/C, zero-dependency,
statically-allocated, synchronous-by-design world Tiger Style was written for.

**This is a living document.** When a rule turns out not to fit this stack,
we change the rule *here* rather than quietly ignoring it. Every deviation
below is deliberate and explained — if you hit a new one, add it, don't just
work around it silently.

## Naming

- C#: `PascalCase` for types/methods/public members, `camelCase` for locals/
  parameters/private fields (already idiomatic .NET). Razor component files
  `PascalCase.razor` to match the component name.
- Suffix variable names with units/qualifiers, most-significant last
  (`latencyMsMax`, not `maxLatencyMs`). Prefer equal-length paired names
  (`source`/`target` over `src`/`dest`). Sort alphabetically when order is
  otherwise arbitrary. Prefix helper functions with their caller's name to
  show call history.

## Safety & assertions

- Use `Debug.Assert` and explicit `ArgumentException`/guard checks liberally
  — pre/post-conditions, both "positive space" (what must be true) and
  "negative space" (what must never happen). Pair assertions across a
  boundary (e.g. assert before a Neo4j write and again after reading it
  back).
- Don't swallow exceptions silently — no empty `catch {}` blocks. Handle
  errors explicitly (typed result, rethrow with context, or a logged and
  deliberate fallback), never a silent pass-through.
- Nullable reference types are enabled project-wide (`<Nullable>enable</Nullable>`)
  — treat `!`-suppression as a smell that needs a comment justifying it, not
  a routine escape hatch.
- Fixed upper bound on all loops/queues — kept, and load-bearing: the
  Batman Number BFS must have an explicit max-depth bound, or a query
  between two loosely-connected Characters can walk the entire graph.
- No recursion for graph traversal — an explicit queue/stack, not language
  recursion, so depth bounds stay enforceable.

## Control flow

- Push `if`s up, `for`s down; split compound conditions into nested
  `if`/`else`; centralize branching in one place and keep leaf functions
  non-branchy. Applies to both C# and Razor markup — extract branchy logic
  out of `.razor` files into named methods/services rather than nesting it
  in markup.
- **Dropped/reinterpreted**: "functions run to completion without
  suspending" is incompatible with `async`/`await` by construction —
  suspension *is* the point. Reinterpreted as: an async method should
  represent one clear unit of async work with obvious suspension points,
  not suspend conditionally scattered through deep branching.

## Functions & file size

- Hard limit: 70 lines per method/function. No automated enforcement wired
  up yet (Rust had `clippy::too_many_lines`; the .NET analyzer equivalent —
  e.g. a Roslyn analyzer/`.editorconfig` rule — is a TODO, not a rejected
  idea).
- File-level: 300-line warning, same status (manual for now, not yet
  enforced by tooling).

## Memory & allocation

- **Dropped**: "statically allocate all memory at startup, none after" —
  impossible for an ASP.NET Core app or a browser-rendered SPA; both
  allocate per-request/per-render by construction, correctly so.
- Reinterpreted as: avoid *unnecessary* or *unbounded* allocation — don't
  clone large graph structures per-request when a reference/span works,
  don't grow an unbounded cache, avoid allocation churn in hot loops where
  a preallocated buffer/`ArrayPool` is easy. Declare variables at the
  smallest scope that works.

## Dependencies

- **Dropped**: "zero dependencies except the toolchain" — already
  incompatible with the stack we chose (ASP.NET Core, MudBlazor,
  Neo4j.Driver).
- Reinterpreted as: no *unnecessary* dependency creep. Before adding a new
  NuGet package, check whether the BCL or an existing dependency already
  covers it. Prefer widely-used, actively-maintained packages —
  supply-chain risk is still real even though zero-deps isn't achievable.

## Testing

- Kept in spirit: test valid *and* invalid inputs, write test names that
  state the goal and method, not just the method under test.
- **Out of scope**: TigerBeetle's deterministic simulation testing is a
  large, distinctive investment that doesn't fit a hobby project at this
  stage. Explicitly scoped down, not an oversight.
- **Outer acceptance-testing loop**: deferred, not decided — ADR-0006's
  Cucumber/BDD approach doesn't carry over as-is (see
  [ADR-0009](./adr/0009-dotnet-blazor-stack-pivot.md)). Until an outer-loop
  tool is chosen, acceptance scenarios are plain xUnit/NUnit tests written
  against `CONTEXT.md`'s vocabulary (Character, Connection, Batman Number,
  ...) in their names/structure, not implementation terms.

## Performance

- Kept as a design-time habit: think about Neo4j query cost, network
  round-trips, and batching *before* writing the query, not after it's
  slow. No numeric back-of-envelope budget exists yet — add one once
  ingestion/query patterns are real.

## Formatting

- C#: `dotnet format`, default .NET conventions (4-space indent).
- Comments explain *why*, not *what* — matches this project's general
  convention already (see the root instructions this repo follows).
- `.editorconfig` covers indent width/style, line endings, trailing
  whitespace, and final newline for file types not covered by `dotnet
  format` (YAML, TOML, Markdown, Dockerfiles).
