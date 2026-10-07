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

- **Database**: Neo4j (graph), run via Docker
- **Web**: .NET Blazor Web App (Server interactivity) + [MudBlazor](https://mudblazor.com), [Neo4j.Driver](https://github.com/neo4j/neo4j-dotnet-driver)
- **Ingestion**: a .NET console app (`src/OracleOfBatman.Ingest`) that seeds
  the graph from the [Comic Vine API](https://comicvine.gamespot.com/api/),
  run on demand, not continuously
- Production hosting target is intentionally undecided (config lives in env
  vars, not provider-specific files) — see [ADR-0009](./docs/adr/0009-dotnet-blazor-stack-pivot.md)
  for why the stack moved off Rust/React/Docker-for-everything.

## Repo layout

```
src/
  OracleOfBatman.Domain/  — shared types (Character, Connection, Interaction Tier, ...)
  OracleOfBatman.Web/     — Blazor Web App + MudBlazor, calls Neo4j directly (no separate API tier)
  OracleOfBatman.Ingest/  — one-off/occasional Comic Vine API → Neo4j ingestion console app
docs/adr/   — architecture decision records
```

## Running locally

Everything in Docker ([ADR-0017](./docs/adr/0017-docker-for-the-whole-stack.md)):

```
cp .env.example .env            # fill in your Comic Vine API key
docker compose up -d --build    # Neo4j + web
```

- Web app: http://localhost:8080
- Neo4j Browser: http://localhost:7474

To run the ingestion console app (not started by default):

```
docker compose run --rm ingest --seed-id 1699 --seed-id 1440
```

For development with hot reload, run only Neo4j in Docker and the web app on
the host:

```
docker compose up -d neo4j
dotnet watch --project src/OracleOfBatman.Web run
```

- Web app (dev, hot reload): http://localhost:5204
- The Graph tests use Testcontainers, so Docker must be running for `dotnet test`.

## Deploying

One small VPS running the same compose file plus Caddy for HTTPS — see
[docs/DEPLOY.md](./docs/DEPLOY.md).
