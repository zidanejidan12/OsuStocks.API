# Deployment Plan — Option A (single VPS, DigitalOcean Singapore)

> Status: **planned, not yet executed** (as of 2026-06-07). Hetzner VPS to be created/paid by the owner.
> Companion docs: [`HOSTING_VENDORS.md`](HOSTING_VENDORS.md) (cost/vendor rationale), [`ENVIRONMENT_VARIABLES.md`](ENVIRONMENT_VARIABLES.md) (env contract), [`OPERATIONS_RUNBOOK.md`](OPERATIONS_RUNBOOK.md), [`DEPLOYMENT.md`](DEPLOYMENT.md).

## Decisions (locked)

| Decision | Choice |
|---|---|
| Host | **DigitalOcean Droplet — 8 GB / 2 vCPU / 160 GB NVMe** ($48/mo Basic; $64 for the 4-vCPU tier), **Singapore (SGP1)**, Ubuntu 24.04 LTS — chosen over Hetzner because Hetzner has no APAC region; Singapore is ~20–40 ms from Indonesia vs ~180–300 ms to EU/US. 2 vCPU is fine to start; resize up if CPU-bound. |
| TLS / reverse proxy | **Caddy** (auto Let's Encrypt) — replaces the current `nginx` service |
| Frontend hosting | **Same VPS**, `OsuStocks.Web` added as a `web` container behind Caddy |
| Off-host backups | **Cloudflare R2** (S3-compatible, free tier) |
| osu! OAuth (prod) | **Separate production osu! app** (keeps local dev app 12766→localhost working) |
| DB migrations | Guarded **auto-migrate on API startup in Production** (recommended) — API does NOT auto-migrate today |

## Architecture

```
Internet :80/:443
      │
   ┌──▼───┐  Caddy (auto-TLS)
   │ caddy │  api.<domain> → api:8080 ; app.<domain> → web:3000
   └─┬───┬─┘
 api │   │ web (Next.js, NEXT_PUBLIC_API_BASE_URL baked at build = https://api.<domain>)
 ┌──▼─┐ ┌▼───┐
 │api │ │web │
 └─┬──┘ └────┘
   │ internal compose network only
 ┌─▼──┐ ┌─────┐ ┌──────┐
 │ pg │ │redis│ │worker│   pg dump ──nightly──▶ Cloudflare R2
 └────┘ └─────┘ └──────┘
```
Postgres/Redis are **internal-only** (no published host ports in prod). Everything external goes through Caddy.

## Open inputs still needed (non-secret)
- **Domain** (e.g. `osustocks.com`) → drives Caddyfile, `OSUOAUTH_REDIRECT_URI`, `CORS_ORIGIN`, FE build arg

Region decided: **DigitalOcean Singapore (SGP1)** — closest region to Indonesia (~20–40 ms from Jakarta).

## Pre-flight checklist — owner does these
1. **DigitalOcean**: create a Droplet — 8 GB / 4 vCPU, region **Singapore (SGP1)**, Ubuntu 24.04 — add SSH key, record public IP. (Optional: enable DO weekly Backups, ~+20% ≈ $9.60/mo, in addition to the R2 dumps.)
2. **Domain**: register; add DNS A records `api.<domain>` → VPS IP, `app.<domain>` → VPS IP.
3. **Prod osu! OAuth app** (osu.ppy.sh/home/account/edit#oauth): callback **exactly** `https://api.<domain>/api/v1/auth/callback`; save ClientId + ClientSecret.
4. **Cloudflare R2**: create bucket + API token (Access Key ID / Secret + account endpoint).
5. **UptimeRobot**: account (point at `https://api.<domain>/health` after deploy).

## Secrets to generate → server `.env` (never committed)
- `POSTGRES_PASSWORD` — strong random
- `JWT_SIGNING_KEY` — `openssl rand -base64 48` (≥32 chars or startup aborts)
- `OSUOAUTH_CLIENT_ID` / `OSUOAUTH_CLIENT_SECRET` — prod osu! app
- `OSUOAUTH_REDIRECT_URI=https://api.<domain>/api/v1/auth/callback`
- `CORS_ORIGIN=https://app.<domain>` (feeds CORS + OAuth return-url allow-list)
- `ASPNETCORE_ENVIRONMENT=Production`
- R2: bucket name, account endpoint, access key id, secret (names TBD when backup wiring is added)

Production fail-fast validation aborts boot if any required secret is missing or still contains `replace-with-`.

## Repo changes still TODO (not yet made)
1. **`nginx` → `caddy`** in `docker-compose.yml` + a `Caddyfile` (domain via env, both vhosts, cert volume, ports 80/443).
2. **Add `web` service** for the frontend. `OsuStocks.Web` is a **separate repo** → on the VPS clone **both repos side-by-side**; compose builds FE with `NEXT_PUBLIC_API_BASE_URL=https://api.<domain>` (inlined at build time, so a domain change requires a rebuild).
3. **Lock down ports** — remove public `5432`/`6379`/`5152` mappings (internal network + Caddy only).
4. **Migrations** — add guarded `Database.Migrate()` on API startup in Production (or a one-shot migrator container). Today nothing auto-migrates.
5. **R2 backup** — extend `scripts/pg-backup.sh` to push the dump to R2 (via `rclone`/`aws`), driven by new `.env` vars, nightly cron/systemd timer.
6. **Prod `.env.example` + server runbook** — `ufw` (allow 22/80/443 only), Docker install, clone both repos, `.env`, `docker compose up -d --build`, migrate, verify `https://api.<domain>/health`, wire UptimeRobot.

## Deploy sequence (once the above are built + VPS exists)
1. SSH in; create non-root user; `ufw` allow 22/80/443; install Docker + compose plugin; unattended-upgrades.
2. Clone `OsuStocks.API` and `OsuStocks.Web` side-by-side.
3. Create `.env` from the prod template with real secrets + domain.
4. `docker compose up -d --build` (Caddy provisions TLS automatically once DNS resolves).
5. Confirm migrations applied; `curl https://api.<domain>/health` → Healthy (pg + redis).
6. Open `https://app.<domain>`, log in with osu! end-to-end.
7. Point UptimeRobot at `/health`; verify first nightly R2 backup lands.

## Notes / risks
- osu! apps allow a single callback URL → that's why prod needs its own app.
- `NEXT_PUBLIC_API_BASE_URL` is build-time; rebuild the `web` image if the domain changes.
- Caddy needs DNS to resolve to the VPS **before** first start to issue certs.
- `POSTGRES_PASSWORD` only applies on first volume init; changing later needs `ALTER ROLE`.
- ~$48/mo (DO 8 GB Droplet, Singapore) + ~$11/yr domain; R2/UptimeRobot free tier. Hetzner would be ~$10/mo but has no APAC region → poor latency from Indonesia, so DO Singapore was chosen for proximity. The compose/Caddy/R2 setup is identical regardless of provider.
- `OSUOAUTH_REDIRECT_URI` and the prod osu! app callback must use the final domain over **https**; Caddy provides the cert once DNS points at the Droplet IP.
