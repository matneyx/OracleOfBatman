---
status: accepted
---

# Path/Hop query types, a new Same Issue tier, and an OracleOfBatman.Graph project

Designing what MVP.md ticket 7's Path service actually returns to the UI
surfaced two things that needed fixing before it could be built.

## Path and Hop

The Web UI needs a chain object to visualize a connection, not just a
boolean/count. New Domain types:

```
Hop(Character From, Character To, int? ComicIssueId, InteractionTier Tier, Confidence Confidence)
Path(IReadOnlyList<Character> Characters, IReadOnlyList<Hop> Hops)
```

`BatmanNumber` is `Path.Hops.Count`, computed rather than stored — no need
for a redundant field. Each Hop carries one *representative* Connection
(the pair's existing "strongest tier, earliest date wins" default from
ADR-0007), not every Connection between that pair — matches docs/UI.md's
"simplified summary info," and MVP ticket 8 ships a plain list with no
per-Connection detail view to justify carrying the full list yet. `From`/
`To` are normalized to walk order (not the stored Connection's arbitrary
discovery-order direction) purely for rendering — this is not a semantic
directionality claim, and doesn't change how Same Issue/Shared Scene
(Symmetric) are stored or traversed.

`IGraphStore` (introduced in ADR-0010's implementation) gains
`Task<Path?> FindShortestPathAsync(int characterAId, int characterBId, int maxDepth)`.
A `null` result covers every "not enough data" case MVP.md ticket 7 needs
— unseeded character or no path within `maxDepth` — undifferentiated,
since the plain-list MVP UI has no use for distinguishing them yet.

## A new Interaction Tier: Same Issue

Reviewing what the crawl (ADR-0010) actually produces against CONTEXT.md's
existing 5-tier list surfaced a gap: crawl-created Connections were tagged
`SharedScene`, but "credited on the same issue" doesn't confirm a shared
scene at all — an issue can bundle several unrelated stories (our own real
sample issue XML has five). `SharedScene` was too strong a claim for what
ingestion can actually verify.

New tier, ranked weaker than Shared Scene but stronger than Shared Identity
(weakest → strongest): Shared Identity, **Same Issue**, Meta Mention,
In-Universe Mention, Shared Scene, Direct Interaction. Reasoning: Same
Issue is still real-world evidence of proximity (unlike Shared Identity,
which needs none), but it's less specific than a confirmed Shared Scene.
Storage is a tier name string in Neo4j, not an ordinal, so this reordering
doesn't touch existing data shape.

Consequences carried out alongside this decision:
- `ConnectionCrawler` now produces `InteractionTier.SameIssue` instead of
  `SharedScene`.
- Existing crawl-produced Connections in the live Aura instance (from the
  Jim Hammond/Jeff the Land Shark and Soft Serve/Bloodscream runs) were
  relabeled from `SharedScene` to `SameIssue` via a one-off Cypher update
  — not worth a migration framework for a hobby project at this stage.

## A new project: OracleOfBatman.Graph

`IGraphStore`/`Neo4jGraphWriter` move out of `OracleOfBatman.Ingest` into a
new `OracleOfBatman.Graph` project — pure Neo4j+Domain access, no Comic
Vine knowledge. `OracleOfBatman.Web` needs Neo4j *read* access for the
Path service (ticket 7), and referencing `Ingest` directly (a console
ingestion tool that also carries `ComicVineApiClient`/`ConnectionCrawler`)
would be a strange shape for a web UI to depend on. Both `Ingest` and
`Web` now reference `Graph` instead.
