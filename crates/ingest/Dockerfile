# Build context is the repo root (needed for the Cargo workspace).
FROM rust:1-slim-bookworm AS base
WORKDIR /app

FROM base AS builder
COPY . .
RUN cargo build --release --bin ingest

FROM debian:bookworm-slim AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY --from=builder /app/target/release/ingest /usr/local/bin/ingest
CMD ["ingest"]
