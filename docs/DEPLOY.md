# Deploying to a single VPS

The production setup is the same `docker-compose.yml` you run locally, plus
`docker-compose.prod.yml`, which adds Caddy for HTTPS and stops publishing
Neo4j's and the web app's ports. One small Linux VPS (~4 GB RAM) runs all
three containers. Managed Neo4j's free tier was ruled out because it pauses
idle instances.

## 1. Server

1. Create an Ubuntu VPS and add your SSH key.
2. Point a DNS `A` record (e.g. `oracle.example.com`) at its IP. Caddy can't
   get a certificate until this resolves.
3. Install Docker:

   ```
   curl -fsSL https://get.docker.com | sh
   ```

4. Allow only SSH and web traffic:

   ```
   ufw allow OpenSSH && ufw allow 80 && ufw allow 443 && ufw enable
   ```

## 2. App

```
git clone https://github.com/matneyx/OracleOfBatman.git
cd OracleOfBatman
cp .env.example .env
```

Edit `.env`:

- `SITE_ADDRESS` — the hostname from step 1.2.
- `NEO4J_PASSWORD` — a long random value, never `changeme`.
- `COMIC_VINE_API_KEY` — your key.

## 3. Data

Copy the local graph up so the site doesn't start empty. Locally (stops
Neo4j for a few seconds):

```
docker compose stop web neo4j
docker compose run --rm --no-deps -v "$PWD/backup:/backups" neo4j \
  neo4j-admin database dump neo4j --to-path=/backups --overwrite-destination=true
docker compose up -d
scp backup/neo4j.dump you@server:~/OracleOfBatman/backup/
```

On the server, before the first start (or with Neo4j stopped):

```
docker compose run --rm --no-deps -v "$PWD/backup:/backups" neo4j \
  neo4j-admin database load neo4j --from-path=/backups --overwrite-destination=true
```

The dump carries the data, not the password: the server's Neo4j uses
`NEO4J_PASSWORD` from its own `.env`.

## 4. Start

```
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build --wait
```

Then open `https://<SITE_ADDRESS>`. Logs:

```
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f web caddy
```

## Updating

```
git pull
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build --wait
```

Neo4j data and the web app's data-protection keys live in named volumes, so
rebuilds keep them.
