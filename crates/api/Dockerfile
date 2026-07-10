# Build context is the repo root (needed for the Cargo workspace).
FROM rust:1-slim-bookworm AS base
WORKDIR /app

FROM base AS builder
COPY . .
RUN cargo build --release --bin api

FROM debian:bookworm-slim AS runtime
COPY --from=builder /app/target/release/api /usr/local/bin/api
CMD ["api"]
