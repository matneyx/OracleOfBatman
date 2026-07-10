# Oracle of Batman

Six degrees of separation for fictional characters — find the shortest path
of **Connections** between any two **Characters**, and their **Batman
Number**. Like Oracle of Bacon, but for comics (and eventually novels,
webcomics, film, and TV).

See [CONTEXT.md](./CONTEXT.md) for the domain glossary,
[docs/adr/](./docs/adr/) for architecture decisions and why they were made,
[docs/STYLE.md](./docs/STYLE.md) for the (Tiger Style-inspired) engineering
discipline this repo follows, [docs/MVP.md](./docs/MVP.md) for the current
MVP scope and ticket list, [docs/UI.md](./docs/UI.md) for the full UI
vision, and [docs/POST_MVP.md](./docs/POST_MVP.md) for deferred ideas not
yet scheduled.

## Stack

- **Database**: Neo4j (graph)
- **Backend**: Rust, [Axum](https://github.com/tokio-rs/axum), [neo4rs](https://github.com/neo4j-labs/neo4rs)
- **Frontend**: React + [HeroUI](https://heroui.com), built with Vite
- **Ingestion**: a Rust CLI (`crates/ingest`) that seeds the graph from the
  [Comic Vine API](https://comicvine.gamespot.com/api/), run on demand, not continuously
- Everything runs in Docker; production hosting target is intentionally
  undecided (config lives in env vars, not provider-specific files)

## Repo layout

```
crates/
  domain/   — shared types (Character, Connection, Interaction Tier, ...)
  api/      — Axum HTTP API
  ingest/   — one-off/occasional Comic Vine API → Neo4j ingestion CLI
frontend/   — Vite + React + HeroUI SPA
docs/adr/   — architecture decision records
```

## Running locally

```
cp .env.example .env   # fill in your Comic Vine API key if you're running ingest
docker compose up
```

- Frontend (dev, hot reload): http://localhost:5173
- API: http://localhost:8080
- Neo4j Browser: http://localhost:7474

To run the ingestion CLI (not started by default):

```
docker compose --profile ingestion run --rm ingest
```

`docker-compose.yml` is the production-shaped base (built images, no source
mounts); `docker-compose.override.yml` is merged in automatically for local
dev (source-mounted, hot-reloading).
