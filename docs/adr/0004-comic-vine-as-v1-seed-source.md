# Comic Vine replaces Marvel API as the v1 seed data source

Marvel's official developer API (developer.marvel.com) has been discontinued — no
official replacement, only community-sourced workarounds. We're switching the v1
seed source to the [Comic Vine API](https://comicvine.gamespot.com/api/) instead.

This doesn't touch ADR-0002's core scope decision (the graph is publisher- and
medium-agnostic; the seed source was never meant to be a hard boundary) — it just
updates which API that seed source is. If anything, Comic Vine fits ADR-0002
better than Marvel ever did: it already indexes multiple publishers in one data
source, rather than requiring a second ingestion source later to go beyond Marvel.

Trade-offs accepted going in:
- **Non-commercial use only** per Comic Vine's terms — fine for a hobby project,
  but blocks ever monetizing directly on their data if that changes.
- **Rate limits**: 200 requests/resource/hour plus velocity-based throttling —
  ingestion needs real caching/backoff, not naive per-character request loops.
- Comic Vine's own developer community reports inconsistent documentation quality
  — expect some trial-and-error rather than clean docs.
