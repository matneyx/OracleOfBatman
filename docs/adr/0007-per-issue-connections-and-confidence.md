# Connections are per-issue records with a Confidence, crawl uses friends/enemies first

Two related schema/algorithm decisions, arrived at together.

**A Connection is an atomic, per-issue record, not an aggregate.** Earlier
design treated a Connection as carrying "one or more" Interaction Tier
records between a Character pair. In practice, Comic Vine only ever tells
you two Characters share an issue — it can't tell you *how* they interacted
in it. Modeling each shared issue as its own Connection (referencing exactly
that one issue, or none for a Shared Identity Connection) is both more
honest about what the data actually says and a natural fit for Neo4j, which
supports multiple parallel relationships between the same two nodes without
any contortion. A Character pair with a long shared history (Batman/Joker)
can have hundreds of Connections; the pair's "default" Connection for
pathfinding/display is a selection rule over that set — strongest
Interaction Tier, tied-broken by earliest publication date — not a property
of any single edge.

**Every Connection ingestion produces is Unverified.** Same-issue
co-occurrence is a low-confidence signal: it doesn't distinguish a real
Direct Interaction from two Characters who merely share a cover. Added a
Confidence property (`Unverified`/`Verified`) so this uncertainty is
represented honestly in the data rather than silently assumed away. A human
verifying a Connection (turning it `Verified` and confirming its real
Interaction Tier) is a curation UI — out of scope for MVP, tracked in
`docs/POST_MVP.md`. This is the same gap the Snoopy → Snoop Dogg canonicity
question was gesturing at (`docs/POST_MVP.md`) — both needed a way to say
"this link exists in the data, but its trustworthiness is an open question."

**The crawl checks cheap signals before expensive ones.** Comic Vine's
character resource returns `character_friends`/`character_enemies` on the
same response used to fetch a character's issues — free, curated,
relationship data with no extra request. The crawl checks seed-to-seed
issue overlap first, then friend/enemy overlap (still free), and only
expands into fetching *other* characters' issues/relationships — the
expensive step, given the 200 requests/resource/hour limit — if neither
cheap check finds a connection. Issue detail fetches also pull
`character_credits` (full per-issue cast), surfacing further bridge
candidates without additional per-character requests.
