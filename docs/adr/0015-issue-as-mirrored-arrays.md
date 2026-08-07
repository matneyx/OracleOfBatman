---
status: accepted
---

# Issue as mirrored arrays, not edges; lazy materialization on confirmed overlap

Design-only ADR, like the ones it supersedes parts of — not yet
implemented. Supersedes ADR-0013's storage mechanism (`CREDITED_IN` edges)
and pathfinding approach specifically; ADR-0013's problem statement,
"Same Issue becomes structural," and "keep `Connection` for future tiers"
all still hold. Also refines ADR-0014's escalation ladder.

Reached by grilling through a half-forced, ad-hoc start on ADR-0013 — the
user had already added an `Issue` Domain record and reshaped `Hop` to
carry it before this ADR existed; this document is the design the
implementation should now converge on.

## `Hop` drops `Tier`/`Confidence` entirely

Not deferred to "carry a null" — removed from `Hop`'s shape. Every real
`Hop` today is a Same Issue hop (confirmed by grep, unchanged since
ADR-0013); a field that only ever holds one value is dead weight. This is
the same YAGNI shape as ADR-0014's mixed-hop-length deferral: when a
second Interaction Tier actually gets built, `Hop` will need another look
regardless (likely two variants — an issue-mediated hop vs. a direct-
Connection hop — not just "add the field back"). Flagged explicitly here
so it reads as a deliberate deferral, not a regression against ADR-0013's
"keep `Connection`/`Tier`/`Confidence` for future tiers" line — that line
is about `IGraphStore`, not `Hop`, and still holds.

## `Issue` becomes a real Domain type and a real Neo4j node

New file, `Issue.cs` (matching every other Domain type's one-file
convention):

```csharp
public sealed record Issue(int ComicVineId, string? Name, string? ImageUrl = null,
    string? SiteDetailUrl = null, int? VolumeId = null, string? VolumeName = null);
```

`Name` is nullable — unlike `Character.Name` (never actually blank in
practice), `Issue.Name` demonstrably can be (the whole reason the
blank/TPB fallback exists, MVP.md ticket 10). `VolumeId`/`VolumeName` ride
along on `Issue` rather than becoming their own type/node: Comic Vine's
`/volume/{id}/` is a real, separate endpoint, but the issue response
already embeds the volume's id, name, and site-detail-url directly (real
sample data, `docs/raw-api-responses/issue-example.xml`):

```xml
<volume>
    <id>139047</id>
    <name><![CDATA[It's Jeff Infinity Comic]]></name>
    <site_detail_url>...</site_detail_url>
</volume>
```

So there is no reason to ever call `/volume/{id}/` — everything needed
(just the name and id; no value identified in storing more) comes free
from the same `/issue/{id}/` fetch already being made for other reasons
(see enrichment, below).

## Storage: mirrored arrays, not edges

```
(:Character {comic_vine_id, name, image_url, site_detail_url, issue_credits})
(:Issue {comic_vine_id, name, image_url, site_detail_url, volume_id, volume_name, character_credits})
```

No relationship between them at all. `Character.issue_credits` (already
exists today) gets a counterpart `Issue.character_credits` — both arrays
of the other side's `comic_vine_id`s, written together (a double-write)
whenever a credit is recorded. This still solves ADR-0013's original
O(N²) problem (pairwise `CONNECTION` edges between every Character sharing
an Issue) exactly as well as edges would — that problem is about avoiding
per-*pair* writes, and arrays are O(N) per Issue same as edges would be.
The edge-vs-array choice is a separate, secondary question: whether Neo4j
traversal does the work, or the app does manual array-containment joins
(the shape `FindOverlappingIssuesAsync` already uses today for
`Character.issue_credits`). Arrays were chosen deliberately: **it means
storing less data**, which is the project's biggest current bottleneck —
not every Issue in Comic Vine's entire catalog needs a materialized node
just because one Character happens to be credited on it.

## Materialization is lazy — only on confirmed overlap

Two distinct layers, not one:

1. **Raw `issue_credits`** — written unconditionally on every Character
   ingest, exactly as today. Cheap; no `Issue` node required to exist.
2. **Materialized `Issue` node** (with its own `character_credits`
   array) — only created once an overlap between two *already-ingested*
   Characters' raw `issue_credits` arrays is actually confirmed (the
   existing `FindOverlappingIssuesAsync`-shaped check). A Character's own
   raw credit on some issue that nobody else has ever been found on just
   sits in their own array, unmaterialized, indefinitely — no node, no
   cost, until a second Character shows up there too.

Concrete example that drove this design: Character A is credited on
Issues {1, 2, 3}; Character B only on {1}; Character C only on {4}. If
Issues 2 and 4 both already have some accumulated `character_credits`
that happen to include an uningested Character D, D becomes a "strong
candidate" (ADR-0014's existing 2+-appearances rule) — worth fetching
*without ever having paid a Comic Vine request just to notice D*, since
the arrays accumulated for free as a side effect of earlier, unrelated
ingestions. D still needs a real ingest (one `/character/{id}/` fetch) to
become a proper node before the path is genuinely traversable — the
array intersection tells us D is *worth fetching*, not that D is already
present.

## Character ingestion: exactly three triggers

1. **The two seeds** — unconditional (ADR-0010 step 1).
2. **Strong candidates** — appears in 2+ already-*materialized* Issue/
   Team `character_credits`/roster arrays and can bridge the seeds
   (ADR-0014's existing rule, sharpened: "already-relevant" means already
   materialized, not every issue anyone's ever touched).
3. **Frontier expansion** — friends/enemies/teams BFS (ADR-0010, extended
   by ADR-0014 to include teams).

No other path ingests a Character. This was confirmed as a complete,
correct list during design — no fourth case identified.

## ADR-0014's escalation ladder, refined: check free data before paying for it

ADR-0014's step 4 (issue/team-cast bridge discovery) assumed the only way
to see a full cast was a fresh, paid `/issue/{id}/` or `/team/{id}/`
fetch. That's no longer true: any materialized Issue/Team already has
*some* accumulated `character_credits`/roster from ADR-0014's own strong-
candidate mechanism, populated as a side effect of ingesting other,
unrelated characters over the app's whole history — not just this one
query. Step 4 now checks that already-accumulated data **first** (free,
already in Neo4j) and only falls back to a fresh Comic Vine fetch (paid)
if it doesn't turn up enough candidates. Free-then-paid, not always-paid.

## One unified Issue-enrichment fetch, not two separate mechanisms

Before this ADR, two separate things independently called
`/issue/{id}/`: the Volume/TPB-name fallback (MVP.md ticket 10, gated on
blank/TPB name) and the thumbnail lazy-fetch (`Home.razor`'s
`_issueThumbnails` dictionary, gated on "about to be displayed"). Both
hit the same endpoint for different reasons and neither knew about the
other — the live bug this ADR fixes: `IssueCard.Issue.ImageUrl` was never
populated by either, because the thumbnail fetch's result never flowed
back into the `Issue` object once `IssueCard`'s parameter changed from
loose scalars to a whole `Issue`.

These become **one** enrichment step: whenever an `Issue` is about to be
rendered and its `ImageUrl` is `null`, fetch `/issue/{id}/` once, populate
`ImageUrl`, `VolumeId`, `VolumeName`, and `Name` (if still blank) together
from that single response, then write the result back onto the `Issue`
node in Neo4j so it is never re-fetched for anyone, ever again. Triggered
by `ImageUrl is null` specifically (not by `Name`), since the user wants
images loaded on render for every Issue regardless of whether its name
needed the fallback — gating behind `Name` alone would silently skip
images for well-named Issues.

## Display: always show the Volume

Supersedes MVP.md ticket 10's original rule (Volume shown only for
blank/TPB names). Now: always `"{VolumeName}: {Name}"`, or `VolumeName`
alone if `Name` is blank. Free to do since the enrichment fetch above is
now unconditional (fires for every Issue on first render, not gated
behind a blank/TPB check) — `VolumeName`/`VolumeId` come along for free
in the same response that supplies `ImageUrl`.

## Pathfinding: application-level BFS, not a single Cypher query

The biggest accepted consequence. ADR-0013's Bacon-Number pathfinding
(`shortestPath()` over `CREDITED_IN*`, divide relationship count by 2)
relied entirely on `CREDITED_IN` being a real graph edge. Arrays are
property values, not edges — Neo4j's native `shortestPath()` cannot
traverse them. `FindShortestPathAsync` becomes a multi-round-trip BFS
orchestrated from C#: fetch a Character's `issue_credits`, look up each
of those (materialized) Issues' `character_credits` as one adjacency
step, repeat — the same shape `FakeGraphStore`'s existing in-memory BFS
and `ConnectionCrawler`'s own bidirectional crawl already use, just
backed by real Neo4j lookups instead of an in-memory dictionary or a
single Cypher traversal. Accepted deliberately: the storage savings from
arrays-over-edges is the point of this whole ADR, and this is the real
cost of that choice, not a surprise to be discovered mid-build.

## IGraphStore shape (sketch, not final code)

- `UpsertCharacterIssueCreditsAsync` — same idea as today, but now also
  triggers the double-write: for each credited Issue, `MERGE` the Issue
  node (if a confirmed overlap exists — see materialization rule above)
  and push both sides' ids onto each other's array.
- `FindOverlappingIssuesAsync` — kept (unlike ADR-0013's plan to remove
  it), now doubling as the materialization trigger: an overlap found here
  is what causes an `Issue` node to actually get created.
- `Connection`/`UpsertConnectionAsync` — unchanged, still kept for future
  non-issue tiers (ADR-0013's reasoning still holds).
- `PathExistsAsync`/`FindShortestPathAsync` — same signatures, bodies
  rewritten as application-level BFS over the array properties, per
  above.
- `GetCharacterAsync`/`GetIssueAsync` (new, mirrored) — needed for the
  BFS to look up a node's own arrays each step.

## Migration

Simpler than ADR-0013's plan, per the user's explicit call: existing live
Aura Connection data can just be dropped — Character data is fine, but no
attempt is made to harvest issue names/links from the ~13,445 existing
`CONNECTION` relationships (ADR-0013's migration pass 2). `issue_credits`
arrays already on every Character are the complete, authoritative
starting point; every `Issue` node gets (re)created lazily the normal
way, the next time each pair's overlap gets confirmed again, with names/
links/images coming from the new unified enrichment fetch rather than
harvested from soon-to-be-deleted edges. Delete every `CONNECTION`
relationship; keep every `Character` node and its `issue_credits` array
as-is.
