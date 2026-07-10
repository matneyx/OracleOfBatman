---
status: accepted (living — amend as incompatibilities are found)
---

# Adopt a Tiger-Style-inspired engineering discipline, repo-wide

We're adopting [TigerBeetle's Tiger Style](https://github.com/tigerbeetle/tigerbeetle/blob/main/docs/TIGER_STYLE.md)
as our engineering discipline across both the Rust backend and the React/TypeScript
frontend, rather than treating it as backend-only. The full adapted ruleset,
including what's kept as-is, reinterpreted, or explicitly dropped, lives in
[docs/STYLE.md](../STYLE.md) — that file is expected to change as we discover
more of the original guide that doesn't fit a Tokio-async, dependency-using,
browser-facing stack (it was written for a synchronous, zero-dependency,
statically-allocated embedded database in Zig/C).

Enforcement: `clippy::too_many_lines` + `clippy::unwrap_used` (workspace
lints) and `rustfmt` on the Rust side; oxlint's `max-lines-per-function`/
`max-lines` and the [`tiger-assert`](https://github.com/eugeny-dementev/tiger-assert)
library for runtime assertions on the frontend side.

We're doing this despite most of the guide's headline constraints (zero
dependencies, static allocation, no suspension) being incompatible with our
stack, because the parts that *do* transfer — assertion density, small
functions, explicit error handling, bounded loops — catch real bugs and
we'd rather adopt them deliberately from day one than retrofit them later.
