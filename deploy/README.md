# Deploying OsuStocks (Hetzner CX33)

The whole stack — backend, worker, frontend, Postgres, Redis, Caddy (auto‑TLS),
nightly backups, and Prometheus/Grafana observability — runs from
`docker-compose.prod.yml` on a single box. Only **Caddy** publishes host ports
(80/443); everything else is internal to the compose network.

**Domains** (all → the server's IP, DNS at Porkbun):

| Host | Serves |
|------|--------|
| `osustocks.com` (apex) | **Frontend** (the `web` service) — canonical site |
| `app.osustocks.com` | 301 redirect → apex (legacy) |
| `api.osustocks.com` | Backend API + the osu! OAuth callback |
| `grafana.osustocks.com` | Grafana |

The frontend (**OsuStocks.Web**) is a separate repo cloned as a **sibling** of this
one; the `web` service builds it from `../OsuStocks.Web`.

---

## First-time setup

### 0. Secure the server
SSH in as `root`, install git, clone, and bootstrap:
```bash
apt-get update && apt-get install -y git
git clone https://github.com/zidanejidan12/OsuStocks.API.git
cd OsuStocks.API
SSH_PUBKEY="ssh-ed25519 AAAA... you@laptop" bash scripts/server-bootstrap.sh
```
Creates a `deploy` sudo user, installs Docker + Compose, enables `ufw` (22/80/443)
+ `fail2ban`, adds swap. Then test `ssh deploy@<ip>` and run the SSH‑hardening block
the script prints. Also set the **Hetzner Cloud Firewall** to 22/80/443 only.

### 1. DNS (Porkbun)
A records → the server IP for the apex `osustocks.com`, `api.`, `grafana.`, and
`app.` (the last just to power the redirect). The apex A record (host left blank)
is required before Caddy can issue the apex cert.

### 2. Code + secrets (as the `deploy` user)
Clone **both** repos as siblings, then fill in secrets:
```bash
git clone https://github.com/zidanejidan12/OsuStocks.API.git
git clone https://github.com/zidanejidan12/OsuStocks.Web.git   # sibling — the `web` service builds this
cd OsuStocks.API
cp .env.prod.example .env
# edit .env: POSTGRES_PASSWORD, OSUOAUTH_*, JWT_SIGNING_KEY, GRAFANA_ADMIN_PASSWORD,
#            APP_ORIGIN=https://osustocks.com
```

### 3. osu! OAuth (production)
Create an app at <https://osu.ppy.sh/home/account/edit> with callback
`https://api.osustocks.com/api/v1/auth/callback` (this stays on the `api.`
subdomain regardless of the frontend domain), and put the client id/secret +
redirect URI into `.env`.

### 4. First bring-up + migrate
```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
./deploy/deploy.sh --no-pull --migrate     # applies the EF schema (first run)
```

### 5. Make an admin
OAuth creates `User` accounts. Promote yourself (then log out/in — the role is baked
into the JWT at login):
```bash
docker compose -f docker-compose.prod.yml exec postgres \
  psql -U osu_stocks -d osu_stocks -c "UPDATE users SET role='Admin' WHERE username='YOUR_OSU_NAME';"
```

---

## Day-to-day deploys

One command (run from the repo, replaces the manual pull/build/up dance):
```bash
./deploy/deploy.sh             # pull both repos, rebuild + recreate api/worker/web, health-check
./deploy/deploy.sh --migrate   # also apply EF migrations — use when a merged PR adds one
./deploy/deploy.sh --caddy     # also force-recreate caddy — use after editing deploy/Caddyfile
./deploy/deploy.sh --no-pull   # deploy the current checkout without pulling
```
It pulls `OsuStocks.API` + the sibling `OsuStocks.Web`, builds `api`/`worker`/`web`,
optionally migrates, `up -d`, and polls `/health`. (First time: `chmod +x deploy/deploy.sh`,
or run `bash deploy/deploy.sh`.) The PR description says when `--migrate` is needed.

> No auto‑migrate on startup by design (api + worker would race). `--migrate` runs a
> one‑off `dotnet/sdk:9.0` container; it's idempotent (applies only pending migrations).

### One-click deploy from GitHub (optional)
There's a **manual** GitHub Actions workflow (`.github/workflows/deploy.yml`):
**Actions → "Deploy (prod)" → Run workflow**. It SSHes in and runs `deploy.sh` for
you — same script, no SSH on your end. It does **not** fire on merge; you press the
button. Tick **migrate** only when a merged PR added a migration (off by default), and
**caddy** only after editing `deploy/Caddyfile`.

Set these repo **Settings → Secrets and variables → Actions** secrets once:
`SSH_HOST` (server IP), `SSH_USER` (`deploy`), `SSH_PRIVATE_KEY` (a key authorized for
that user — generate a dedicated deploy key and add its public half to
`~deploy/.ssh/authorized_keys`).

## Verify
- `https://api.osustocks.com/health` → `200` (Postgres + Redis green). `/swagger` is off in prod.
- `https://osustocks.com` loads the frontend; `https://app.osustocks.com` 301s to it.
- `https://grafana.osustocks.com` → log in as `admin` / `GRAFANA_ADMIN_PASSWORD`.

## Edge hardening (Caddy)

Caddy is a **custom build** (`deploy/Caddy.Dockerfile`, via `xcaddy`) bundling the
`caddy-ratelimit` and Coraza (OWASP CRS) WAF plugins; stock Caddy ships neither. Configured in
`deploy/Caddyfile`:

- **Global per-IP rate limit** on every route — `api.` 120 req/min, apex 240 req/min, keyed by
  client IP. This is the blanket backstop in front of the app's own per-IP limits (which only cover
  `/auth` and `/trading`).
- **Body + header size caps** — 1 MB request body, 16 KB headers (the API only takes small JSON).
- **WAF (OWASP Core Rule Set via Coraza)** — starts in **`DetectionOnly`** so it *logs* attacks
  without blocking. Once `docker compose -f docker-compose.prod.yml logs caddy` looks clean of
  false positives, flip `SecRuleEngine DetectionOnly` → `On` in `deploy/Caddyfile` and redeploy.
- **Bot / User-Agent filtering** — known scanners (sqlmap, nikto, nuclei, …) and empty
  User-Agents get a `403`. fail2ban (installed by `scripts/server-bootstrap.sh`) remains the
  host-level complement. `grafana.` is intentionally left un-hardened (own auth; WAF/UA rules can
  break its API/websocket traffic).

Apply changes with `./deploy/deploy.sh --caddy` (now rebuilds the image **and** force-recreates the
container). **Validate before deploying:**
`caddy validate --config deploy/Caddyfile --adapter caddyfile` (or `caddy adapt`). All limits/sizes
are tunable; if the custom build fails on a plugin version, pin/adjust the module versions in
`deploy/Caddy.Dockerfile`.

## Backups
The `db-backup` sidecar runs a nightly `pg_dump` into the `postgres_backups` volume,
keeping 7 daily / 4 weekly / 6 monthly copies. Force one / list / restore:
```bash
docker compose -f docker-compose.prod.yml exec db-backup /backup.sh
docker compose -f docker-compose.prod.yml exec db-backup ls -lh /backups/last
# restore:
docker compose -f docker-compose.prod.yml exec db-backup sh -c \
  "gunzip -c /backups/last/osu_stocks-latest.sql.gz | psql -U osu_stocks -d osu_stocks"
```

## Observability / Grafana
The app pushes OpenTelemetry **metrics over OTLP** to Prometheus
(`OTEL_EXPORTER_OTLP_ENDPOINT=http://prometheus:9090/api/v1/otlp`,
`http/protobuf`); host/DB/Redis come from the node/postgres/redis exporters.
- **Connections → Data sources → Add Prometheus**, URL `http://prometheus:9090`.
- Import dashboards by ID: **1860** (Node Exporter), **9628** (PostgreSQL), **763** (Redis).
- App metrics already exported: the **osu! API** meter (`osu_api_*`, incl. `outcome="rate_limited"`
  for 429s) and the **economy** gauges (`economy_credits_circulating|minted|burned`).
- Search Console: verify `osustocks.com` (DNS TXT) and submit `https://osustocks.com/sitemap.xml`.

## Notes & trade-offs
- **Metrics still TODO:** HTTP request rate/latency, .NET runtime/GC, and the Hangfire
  job meter — add `AddAspNetCoreInstrumentation/AddHttpClientInstrumentation/AddRuntimeInstrumentation`
  in `ObservabilityExtensions.cs`.
- **Security:** only Caddy publishes host ports; keep DB/Redis/Prometheus/exporters internal.
- **Latency:** Hetzner has no Asia DC (DE/FI/US) — ~150–250 ms to Indonesian players.
- **osu! API rate:** prod runs `OsuApi__RequestsPerMinute` from config; if Grafana shows
  rising `osu_api_requests_total{outcome="rate_limited"}`, lower it (per-process) on api+worker.
- **Migrations leave root-owned `obj/bin`** in the repo (the SDK container runs as root); if a
  later `git pull` fails on permissions, `sudo chown -R deploy:deploy .`.
