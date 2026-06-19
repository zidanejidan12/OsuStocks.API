# Deploying OsuStocks on a fresh Hetzner CX33

From a bare server + the `osustocks.com` domain to a live, HTTPS site with Grafana.
The whole backend + observability stack runs from `docker-compose.prod.yml`.
(The frontend, OsuStocks.Web, deploys separately — see step 7.)

---

## 0. Secure the server
Add your SSH public key in the Hetzner console (Server → SSH Keys) when creating
the box, then SSH in as `root` and bootstrap (a bare box may lack `git`, and the
script lives in the repo, so install + clone first):
```bash
apt-get update && apt-get install -y git
git clone https://github.com/zidanejidan12/OsuStocks.API.git
cd OsuStocks.API
# SSH_PUBKEY installs your key for the unprivileged 'deploy' user the script creates
SSH_PUBKEY="ssh-ed25519 AAAA... you@laptop" bash scripts/server-bootstrap.sh
```
This creates a `deploy` sudo user, installs Docker + Compose, enables the `ufw`
firewall (22/80/443 only) + `fail2ban`, and adds swap. **Then** test
`ssh deploy@<ip>` from your laptop and run the optional SSH-hardening block the
script prints (disables root/password login) — only after the new login works.

> Private repo? The `git clone` will prompt for auth — use a PAT
> (`git clone https://<token>@github.com/zidanejidan12/OsuStocks.API.git`).

> Also set the **Hetzner Cloud Firewall** in the console to allow only 22/80/443.

## 1. DNS
At your registrar, create **A records → your server IP**:
| Host | Purpose |
|------|---------|
| `api.osustocks.com` | backend |
| `grafana.osustocks.com` | Grafana |
| `app.osustocks.com` | frontend (deployed separately) |

## 2. Get the code + secrets
As the `deploy` user:
```bash
git clone https://github.com/zidanejidan12/OsuStocks.API.git
cd OsuStocks.API
cp .env.prod.example .env
# edit .env: POSTGRES_PASSWORD, OSUOAUTH_*, JWT_SIGNING_KEY, GRAFANA_ADMIN_PASSWORD, APP_ORIGIN
```

## 3. osu! OAuth (production)
Create an OAuth app at <https://osu.ppy.sh/home/account/edit> with callback
`https://api.osustocks.com/api/v1/auth/callback`, and put the client id/secret +
that redirect URI into `.env`.

## 4. Bring up the stack
```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```
This starts postgres, redis, api, worker, caddy (auto‑TLS), prometheus, grafana,
and the node/postgres/redis exporters. Caddy fetches Let's Encrypt certs for the
subdomains (DNS must already resolve to the box).

## 5. Apply database migrations
The app does **not** auto‑migrate. Run once (and after each deploy with new migrations):
```bash
source .env
docker run --rm --network osustocks_default -v "$PWD":/src -w /src \
  -e ConnectionStrings__Postgres="Host=postgres;Port=5432;Database=osu_stocks;Username=osu_stocks;Password=$POSTGRES_PASSWORD" \
  mcr.microsoft.com/dotnet/sdk:9.0 bash -lc \
  "dotnet tool install --global dotnet-ef >/dev/null 2>&1; export PATH=\$PATH:/root/.dotnet/tools; \
   dotnet ef database update --project src/Infrastructure/OsuStocks.Infrastructure.csproj \
   --startup-project src/Api/OsuStocks.Api.csproj"
```
> Cleaner long‑term: add `dbContext.Database.Migrate()` on API startup (with a
> single migrator so api+worker don't race) so deploys self‑migrate. Ask and I'll wire it.

## 6. Verify
- `https://api.osustocks.com/health` → `200` healthy (Postgres + Redis green).
- `https://grafana.osustocks.com` → log in as `admin` / `GRAFANA_ADMIN_PASSWORD`.
- In Grafana: **Connections → Data sources → Add Prometheus**, URL `http://prometheus:9090`.
- Import dashboards (Dashboards → Import by ID): **1860** (Node Exporter Full),
  **9628** (PostgreSQL), **763** (Redis). Build a custom one for the app's osu! API
  metrics (and trades/economy) on top.

## 7. Frontend
OsuStocks.Web (Next.js) is a separate app. Deploy it as a container named `web`
on this network (then uncomment the `app.osustocks.com` block in `deploy/Caddyfile`)
or host it elsewhere (Vercel/another box) pointing `app.osustocks.com` at it.
Either way set `APP_ORIGIN` in `.env` to its HTTPS origin for CORS.

---

## Notes & trade‑offs
- **Metrics today:** the app currently exports only the **osu! API** metrics meter via
  OTLP. Host/DB/Redis come from the exporters above. To also get HTTP request
  rate/latency, .NET runtime/GC, and the Hangfire sync‑job meter, add
  `AddAspNetCoreInstrumentation/AddHttpClientInstrumentation/AddRuntimeInstrumentation`
  + `AddMeter(...)` in `ObservabilityExtensions.cs` (small code change — ask).
- **Security:** only Caddy publishes host ports; Postgres/Redis/Prometheus/exporters
  are internal to the compose network. Keep it that way.
- **Latency:** Hetzner Cloud has no Asia DC (DE/FI/US only) — expect ~150–250 ms to
  Indonesian players, vs the earlier Singapore plan. Fine for launch/beta.
- **Resources:** with `retention=30d` + 30s scrape, the observability stack adds
  roughly ~0.6–0.8 GB RAM on top of the app — comfortable on the CX33's 8 GB.
- **Backups:** add a cron'd `pg_dump` into the `postgres_backups` volume (ask and I'll add a job).
