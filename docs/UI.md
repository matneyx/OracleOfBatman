# UI vision

This is the full vision — most of it is post-MVP polish. MVP ticket 8
(`docs/MVP.md`) ships a plain list rendering of the path with no filters;
everything below is what it grows into.

## Search screen

Two autocomplete text inputs side by side (backed by the character search
endpoint, `docs/MVP.md` ticket 6), a "Go" button, and filters below:

- **Minimum Interaction Tier** — a floor on Interaction Tier strength
  (Direct Interaction strictest, Shared Identity loosest/default —
  default proposed as most-permissive so nothing's hidden unless the user
  narrows it; confirm if a stricter default is wanted).
- **Minimum Confidence** — a floor on Confidence (Verified strictest,
  Unverified loosest/default — same reasoning as above).
- **Minimum Canonicity** — Official (strictest) / **Unofficial (default)**
  / Parody (loosest). Confirmed: Unofficial-and-above counts by default
  (e.g. the non-canon Blondie/Peanuts strip), Parody (e.g. Mad Magazine)
  is opt-in only. See ADR-0008.
- **Allow Shared Identity** (default **true**) — matches how Shared
  Identity was designed; it was never meant to be gated.
- **Allow Different Universe Variations** (default **true**) — off means
  *pinning* a specific Universe, which constrains the entire path (every
  transitional Character's Connection, not just the two search subjects)
  to that one Universe, and requires a Universe picker to appear. Pinning
  makes Shared Identity irrelevant for that query, since Shared Identity
  only ever bridges *different* Universes. See `CONTEXT.md` Relationships.

## Results: node-based diagram

When a path is found: a node-link diagram, one simplified avatar per
Character in the path, simplified summary info on each Connection between
them. Click/hover a Connection for detail: the specific comic issue, and
links out (Comic Vine, Wikipedia, or similar) for further reading.

## No connection found

- **Solo/admin mode**: an "Import from Comic Vine" button that manually
  triggers an `ingest` run for that specific pair — this is a UI-triggered
  invocation of the existing CLI-seeded crawl (ADR-0005), not the
  deferred *automatic* live-crawl-on-any-query idea. No new rate-limit
  risk beyond what the admin already opts into by clicking it.
- **Non-solo/community mode** (speculative, deployment-model-dependent,
  not scheduled): a "request to import this character" flow instead,
  presumably queued for admin review rather than self-service.

## Future (speculative, not scheduled)

- Let a user add Characters/Connections that aren't from any ingested
  source at all.
- "Are these actually the same thing?" compare-and-merge tooling — for
  reconciling e.g. two independently-ingested records that turn out to be
  the same Character, or splitting one that was wrongly merged.
