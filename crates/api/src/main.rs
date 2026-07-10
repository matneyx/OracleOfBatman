#[tokio::main]
async fn main() {
    let port = std::env::var("API_PORT").unwrap_or_else(|_| "8080".to_string());
    let listener = tokio::net::TcpListener::bind(format!("0.0.0.0:{port}"))
        .await
        .expect("failed to bind listener");

    println!("api listening on :{port}");
    axum::serve(listener, api::app()).await.expect("server error");
}
