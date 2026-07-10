---
status: superseded by ADR-0002
---

# Marvel-only universe for v1

We're scoping the graph to Marvel characters/comics only, rather than DC or a multi-publisher graph. Marvel publishes an official public API (developer.marvel.com) with structured character, comic, and creator data, making ingestion tractable without scraping. Publishers don't share continuity, so "one connected universe" only makes sense within a single publisher anyway — mirroring how Oracle of Bacon is scoped to one film industry. DC or other publishers can be added later as a separate universe/graph, not merged into this one.
