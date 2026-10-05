# One Dockerfile, two runnable targets: `web` (the Blazor app, started by
# `docker compose up`) and `ingest` (the crawl console app, run on demand via
# `docker compose run --rm ingest ...`). Both share the restore/build stage so
# the solution only compiles once per image build.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Restore against project files alone first, so source-only edits reuse the
# cached restore layer instead of re-downloading every package.
COPY OracleOfBatman.slnx ./
COPY src/OracleOfBatman.Domain/OracleOfBatman.Domain.csproj src/OracleOfBatman.Domain/
COPY src/OracleOfBatman.Graph/OracleOfBatman.Graph.csproj src/OracleOfBatman.Graph/
COPY src/OracleOfBatman.Web/OracleOfBatman.Web.csproj src/OracleOfBatman.Web/
COPY src/OracleOfBatman.Ingest/OracleOfBatman.Ingest.csproj src/OracleOfBatman.Ingest/
RUN dotnet restore src/OracleOfBatman.Web/OracleOfBatman.Web.csproj \
 && dotnet restore src/OracleOfBatman.Ingest/OracleOfBatman.Ingest.csproj

COPY src/ src/
# No --no-restore: the SDK only adds the framework's web-assets package (which carries
# _framework/blazor.web.js) once it can see the .razor sources, which the csproj-only
# restore above can't. Restoring again here is cheap — packages are already cached.
RUN dotnet publish src/OracleOfBatman.Web/OracleOfBatman.Web.csproj -c Release -o /out/web \
 && dotnet publish src/OracleOfBatman.Ingest/OracleOfBatman.Ingest.csproj -c Release -o /out/ingest

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web
WORKDIR /app
COPY --from=build /out/web ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "OracleOfBatman.Web.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS ingest
WORKDIR /app
COPY --from=build /out/ingest ./
ENTRYPOINT ["dotnet", "OracleOfBatman.Ingest.dll"]
