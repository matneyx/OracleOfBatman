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

```
cp .env.example .env   # fill in your Comic Vine API key if you're running ingest
docker compose up -d   # Neo4j only
dotnet watch --project src/OracleOfBatman.Web run
```

- Web app (dev, hot reload): http://localhost:5204
- Neo4j Browser: http://localhost:7474

To run the ingestion console app (not started by default):

```
dotnet run --project src/OracleOfBatman.Ingest
```
