//! Step definitions for batman_number.feature. Each step is a `todo!()`
//! stub naming the MVP ticket (see /docs/MVP.md) it depends on — this
//! scenario is the spec those tickets build toward, not a test of code
//! that already exists.

use cucumber::{given, then, when, World};

#[derive(Debug, Default, World)]
struct BatmanNumberWorld {
    // Once ticket 2 (Neo4j schema) lands, this holds the testcontainers
    // Neo4j handle + connection; once ticket 7 (path endpoint) lands, it
    // holds the API response.
}

#[given(expr = "a Character {string}")]
async fn a_character(_world: &mut BatmanNumberWorld, _name: String) {
    todo!("needs MVP ticket 1 (domain types) + ticket 5 (ingest CLI wiring)")
}

#[given("they are connected in the graph")]
async fn they_are_connected(_world: &mut BatmanNumberWorld) {
    todo!("needs MVP ticket 2 (Neo4j schema) + ticket 4 (expanding crawl)")
}

#[when(expr = "I request the Batman Number between {string} and {string}")]
async fn request_batman_number(_world: &mut BatmanNumberWorld, _from: String, _to: String) {
    todo!("needs MVP ticket 7 (API path endpoint)")
}

#[then("I should receive a path connecting them")]
async fn should_receive_a_path(_world: &mut BatmanNumberWorld) {
    todo!("needs MVP ticket 7 (API path endpoint)")
}

#[then("the Batman Number should be greater than zero")]
async fn batman_number_greater_than_zero(_world: &mut BatmanNumberWorld) {
    todo!("needs MVP ticket 7 (API path endpoint)")
}

#[tokio::main]
async fn main() {
    BatmanNumberWorld::run("tests/features/batman_number.feature").await;
}
