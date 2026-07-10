# Ingestion is a CLI-seeded expanding crawl, not bulk or live on-demand

`ingest` takes one or more seed characters and a request/depth budget, then
crawls Comic Vine outward from those seeds (comics → co-appearing characters
→ their comics, etc.), writing each discovered Character/Connection into
Neo4j as it goes, stopping once the seeds connect or the budget runs out.
It does not bulk-load Comic Vine's database, and it does not crawl live in
response to an API request.

Bulk ingestion isn't viable: Comic Vine's database is enormous and its rate
limit (200 requests/resource/hour, plus velocity throttling — see ADR-0004)
would never get through a meaningful fraction of it. Live, on-demand crawling
on an API cache-miss was the other option considered — it gives a nicer UX
(any two characters "just work"), but it ties public request latency to a
rate-limited external API and risks a handful of concurrent cold queries
exhausting the hourly quota for everyone; doing it properly needs a
background job/polling pattern, not a blocking request. That's deliberately
deferred, not ruled out — a plausible post-MVP feature once there's a real
job queue to hang it off of.

Consequence: the API only ever answers queries about characters already
seeded into Neo4j. A query for an unseeded pair returns "not enough data
yet," not a live crawl.
