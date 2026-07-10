//! Crawls the Comic Vine API outward from one or more named seed characters
//! (comics -> co-appearing characters -> their comics, ...), writing each
//! discovered Character/Connection into Neo4j as it goes, until the seeds
//! connect or a request/depth budget runs out. Not a bulk loader, and not
//! triggered live by the API — see /docs/adr/0005.
//! Run on demand (see the `ingestion` Compose profile in docker-compose.yml),
//! not as a long-running service.

#[tokio::main]
async fn main() {
    println!("ingest: not yet implemented");
}
