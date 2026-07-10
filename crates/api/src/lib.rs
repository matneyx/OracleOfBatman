use axum::{routing::get, Router};

/// Builds the router without binding a port, so tests (and the e2e crate)
/// can drive it directly instead of spawning a real process.
pub fn app() -> Router {
    Router::new().route("/health", get(|| async { "ok" }))
}
