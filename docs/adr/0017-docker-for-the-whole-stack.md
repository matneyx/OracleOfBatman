---
status: accepted
---

# Run the whole stack in Docker; keep host-side `dotnet watch` for development

Supersedes ADR-0009's "Docker scoped to Neo4j only" clause. The rest of
ADR-0009 (the .NET/Blazor/MudBlazor stack) is unchanged.

ADR-0009 pulled the app out of Docker because the old Rust/npm containers
existed only to hot-reload in-container, which wasn't worth keeping. The
constraint now is different: Docker is the approved way to run this on the
author's work machine. So `docker compose up` should start everything.

- One multi-stage `Dockerfile` with two targets: `web` (aspnet runtime,
  port 8080) and `ingest` (console runtime). Both share one build stage, so
  the solution compiles once.
- `ingest` sits behind a compose profile. `docker compose up` never starts
  it; you run it on demand with `docker compose run --rm ingest ...`, the
  same on-demand model as ADR-0005.
- `web` waits on a Neo4j healthcheck (`cypher-shell RETURN 1`), because it
  runs `EnsureSchemaAsync` at startup and crashes if Bolt isn't up yet.
- Compose overrides `NEO4J_URI` to `bolt://neo4j:7687`. `.env` still holds
  the host-side `localhost` value, so the host workflow keeps working.
- Data-protection keys get their own volume, so antiforgery tokens survive
  a rebuild.

Neo4j's ports stay published. Host-side `dotnet watch` (hot reload) and the
Testcontainers suite are still the development loop; they just need
`docker compose up -d neo4j` instead of the full stack.
