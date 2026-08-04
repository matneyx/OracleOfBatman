---
status: accepted
---

# Persist issue_credits for cross-run overlap detection; accept the partial-graph limitation

Implementing ADR-0011's Path service surfaced two gaps in ADR-0010's crawl
that hadn't been traced through before: both are consequences of ADR-0005's
already-accepted trade-off (no bulk ingestion; the graph is built
incrementally, per-pair, against a rate-limited API), not new mistakes.

## Gap 1 (fixed): overlap checks were scoped to one crawl run, not the graph

`ConnectionCrawler` tracked discovered characters in an in-memory
dictionary, live only for the duration of one `PopulateConnectionsAsync`
call. A newly-fetched character's issues were checked against that run's
discoveries only — never against characters from a *different*, earlier
crawl. Two unrelated crawls (e.g. Jim Hammond/Jeff the Land Shark, then
later Soft Serve/Bloodscream) could each independently discover the same
bridge character (say, some Avenger) without ever cross-checking it against
the *other* crawl's finds, even when a real shared-issue Connection exists
between them.

**Fix**: Character nodes gain a stored `issue_credits` property (the raw
list of Comic Vine issue ids from that character's own fetch — the same
data already used for in-run overlap checks, just no longer discarded
afterward). `IGraphStore` gains:
- `UpsertCharacterIssueCreditsAsync(comicVineId, issueCreditIds)`
- `FindOverlappingIssuesAsync(comicVineId, issueCreditIds)` — returns every
  *other* Character in the entire graph sharing at least one issue, with
  which issue(s), for Connection creation.

`ConnectionCrawler` no longer keeps its own overlap-checking dictionary at
all — since it persists each character immediately after fetching, a plain
query against the graph covers this run's finds *and* every prior run's,
uniformly. It keeps a small in-memory visited-id set purely for frontier/
loop bookkeeping (has this run already fetched this id), which is a
different concern from overlap detection.

Consequence: Characters already ingested before this change (Jim Hammond,
Jeff the Land Shark, Gwenpool, Soft Serve, Beast, Bloodscream, and the rest
of the Soft Serve/Bloodscream crawl's finds) have no stored `issue_credits`
yet, so they won't be found by future cross-run overlap checks until
re-crawled. Not backfilled now — a small, optional follow-up if it matters
later, not blocking.

## Gap 2 (accepted, not fixed): a found path isn't guaranteed to be the shortest possible one

`ConnectionCrawler` stops as soon as *any* path connects the two seeds
(ADR-0010) — it never keeps searching for a *shorter* one once one exists.
`IGraphStore.FindShortestPathAsync` (ADR-0011) is not stale — it recomputes
the true shortest path over *whatever's currently in the graph* on every
call — but the graph itself may be missing a shortcut through a character
neither seed's crawl ever had reason to fetch.

There is no fix for this short of bulk-ingesting Comic Vine's entire
database, which ADR-0005 already rejected as infeasible against a
200-requests/hour limit. **Accepted as a permanent characteristic of an
incrementally-crawled graph**: the Batman Number the Path service returns
is the shortest path *known from data crawled so far*, not a mathematically
guaranteed global shortest path — similar in spirit to any search system
built on a partial index. Worth surfacing in the UI eventually (a post-MVP
concern, not scheduled) so it doesn't read as a stronger claim than it is.
