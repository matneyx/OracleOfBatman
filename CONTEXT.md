# Oracle of Batman

A "six degrees of separation" graph for fictional characters across publishers and media, letting users find the shortest connection path between any two characters (analogous to Oracle of Bacon / Six Degrees of Kevin Bacon).

## Language

**Character**:
The canonical, cross-universe, cross-media identity a user searches for and that connections resolve to by default (e.g., "Bruce Wayne").
_Avoid_: Hero, person, entity

**Universe**:
A specific continuity/canon within a franchise or medium (e.g., Earth-616 comics, the DCEU films, a specific animated series).
_Avoid_: Continuity, canon, timeline

**Portrayal**:
One specific appearance of a Character within one Universe (e.g., "Bruce Wayne, Earth-616" vs. "Bruce Wayne, DCEU").
_Avoid_: Version, incarnation, appearance

**Mantle**:
A shared name/role that multiple different Characters can hold over time (e.g., the "Batman" mantle, held primarily by Bruce Wayne, but also by Azrael, Dick Grayson, etc.).
_Avoid_: Identity, alias, codename

**Title**:
A specific ongoing publication that Issues belong to (e.g., "Mad Magazine," "Amazing Spider-Man"). Can be flagged Parody/Satire, which cascades to the Canonicity of every Connection derived from its Issues, without requiring per-Connection review.
_Avoid_: Series, publication, book

**Connection**:
A single recorded interaction between two Characters, carrying exactly one Interaction Tier and referencing at most one comic issue (a Shared Identity Connection references none). A Character pair can have many Connections — Batman and the Joker plausibly have hundreds, one per issue they've shared — used together to compute shortest paths between Characters.
_Avoid_: Edge, relationship, link (these are fine in implementation, but "Connection" is the domain term)

**Path**:
The ordered sequence of Hops between two queried Characters — what a Batman Number is the length of. Computed by a path query over whatever's already in Neo4j; not itself stored.
_Avoid_: Chain, route

**Hop**:
One step of a Path: two adjacent Characters plus the single representative Connection between them (the Character pair's default Connection — strongest Interaction Tier, tie-broken by earliest date).
_Avoid_: Edge, step (fine in implementation; "Hop" is the domain term for a Path's unit)

**Interaction Tier**:
The strength/nature of a Connection, one of (strongest to weakest): Direct Interaction, Shared Scene, In-Universe Mention, Meta Mention, Same Issue, Shared Identity.
_Avoid_: Link type, crossover type

**Confidence**:
Whether a Connection's Interaction Tier has been confirmed by a human (**Verified**) or is a raw **Same Issue** Connection produced by ingestion (**Unverified**). Ingestion can only ever produce Unverified Connections — knowing two Characters share an issue doesn't tell you whether they actually interacted, shared a scene, or just shared a cover.
_Avoid_: Canonicity, trust level

**Canonicity**:
Whether a Connection's underlying event counts as real/sanctioned within the fiction's own terms, one of (strongest to weakest): Official (a sanctioned crossover), Unofficial (happened on the record, but not canonical — e.g. the Blondie 50th-anniversary strip that featured Peanuts characters despite Charles Schulz's well-known refusal of crossovers), Parody (from a satire/parody Title, e.g. Mad Magazine, where "shared issue" doesn't imply any real relationship at all). Orthogonal to Confidence — a Connection can be Verified and still Unofficial or Parody; Confidence is about whether a human confirmed what's on the page, Canonicity is about whether what's on the page counts as real within the fiction.
_Avoid_: Confidence, trust level, official-ness

**Direct Interaction**:
Two Characters actively interact with each other (speak to, fight, touch, etc.) in the same scene. Symmetric.

**Shared Scene**:
Two Characters are both present in the same scene but do not interact. Symmetric.

**In-Universe Mention**:
One Character is referenced by name within the story world by another, without both being on-page together. Directional.

**Meta Mention**:
One Character breaks the fourth wall to reference something/someone entirely outside their own Universe (e.g., a different franchise, or the real world). Directional; the referenced entity becomes its own Character node rather than a flag (see ADR-0002).

**Same Issue**:
Two Characters are both credited on the same comic issue, with no confirmation they ever shared a scene or even a story within it — an issue can bundle several unrelated stories (e.g. an anthology/Infinity Comic issue). This is the raw signal ingestion actually produces, always paired with **Unverified** Confidence, pending a human either confirming a stronger tier or downgrading it (see `docs/POST_MVP.md`'s curation UI). Symmetric.
_Avoid_: Shared issue, co-occurrence

**Shared Identity**:
Exists automatically between any two Portrayals of the same Character, with no story ever having connected them (e.g., DC's and Marvel's independent takes on Frankenstein's Monster, or two unrelated fictionalizations of a real president). Symmetric; the weakest tier, since it requires no authorial act, just shared canonical identity. A possible future refinement is collapsing same-Character Portrayals into one node instead of linking them this way — deferred for now.

## Relationships

- A **Mantle** is held by one or more **Characters** over time; exactly one Character is the default holder for a given Mantle (see Flagged ambiguities)
- A **Character** has one or more **Portrayals**, each belonging to exactly one **Universe**
- Connections between **Characters** resolve at the Character level by default (collapsed across all Portrayals) — each hop can independently draw from whichever Portrayal/Universe actually has the data, so a single path can freely mix Universes hop to hop
- Pinning a **Universe** constrains the *entire path*, not just the two endpoint Characters — every transitional Character's Connection must also be within that same Universe, not just the search subjects. This makes **Shared Identity** irrelevant for a pinned-Universe query, since Shared Identity only ever bridges *different* Universes
- A Character pair's many **Connections** resolve to one *default* Connection for pathfinding/display: the strongest **Interaction Tier** present, tie-broken by whichever happened earliest in publication history — a lone Direct Interaction outranks any number of weaker Connections, and among equally-strong Connections the earliest wins
- Unlike the other Interaction Tiers, **Shared Identity** requires no in-story event — it exists automatically wherever two Portrayals share a Character. Publisher-owned Characters never need it to connect across publishers (their Portrayals all stay within one publisher's own Universes), but it's what lets literary/public-domain Characters and real-world people bridge independently-owned publishers at all
- Ingestion only ever produces **Unverified** Connections (same-issue co-occurrence); a Connection becomes **Verified** once a human confirms its actual Interaction Tier from the issue itself
- A **Connection** belongs to at most one **Title** (via its Issue); a Title flagged Parody/Satire gives every Connection derived from it Parody **Canonicity** automatically, with no per-Connection review needed
- **Parody**-Canonicity Connections are stored and browsable, but excluded from the default **Batman Number** path — surfaced only through an explicit opt-in (see docs/POST_MVP.md)
- The **Batman Number** between two Characters is the length of the shortest path of **Connections** between them (the project's equivalent of a "Bacon Number")

## Example dialogue

> **Dev:** "If I search 'Batman', which Character do I get?"
> **Domain expert:** "The default holder of the Batman **Mantle** — normally Bruce Wayne, not Azrael — collapsed across all of his **Portrayals**, unless I pin a specific **Universe** or explicitly ask to include everyone who's worn the mantle."

## Flagged ambiguities

- Which **Character** is the default holder of a **Mantle** is determined by a most-appearances/most-prominent heuristic, with a manually curated override available — resolved: heuristic-first, human can override.
