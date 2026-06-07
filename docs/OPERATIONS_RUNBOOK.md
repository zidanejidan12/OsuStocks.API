# Operations Runbook

Practical procedures for whoever runs osu!Stocks in production (sponsor / future operator). Assumes the Docker Compose deployment from `docker-compose.yml`. All commands run from the **project root on the VPS** unless noted.

> Service names: `api` · `worker` · `postgres` · `redis` · `nginx`.
> Companion: [`ENVIRONMENT_VARIABLES.md`](ENVIRONMENT_VARIABLES.md) · [`RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md) · [`DISASTER_RECOVERY.md`](DISASTER_RECOVERY.md).

---

## 1. How to deploy

First deploy or routine update.

```bash
# 1. Ensure .env exists and is filled (see ENVIRONMENT_VARIABLES.md)
# 2. Pull latest code / image tag
git fetch --tags && git checkout vX.Y.Z        # or update the image tags in compose

# 3. Build + start the stack
docker compose up -d --build

# 4. Apply database migrations (the app does NOT auto-migrate)
#    Option A — from a machine with the .NET 9 SDK + dotnet-ef, pointing at the DB:
dotnet ef database update --context AppDbContext \
  --project src/Infrastructure/OsuStocks.Infrastructure.csproj \
  --startup-project src/Api/OsuStocks.Api.csproj \
  --connection "Host=<db-host>;Port=5432;Database=osu_stocks;Username=osu_stocks;Password=<STRONG>"
#    Option B — generate an idempotent SQL script and apply with psql:
#    dotnet ef migrations script --idempotent -o migrate.sql ...   then  psql -f migrate.sql

# 5. Verify
docker compose ps
curl -fsS https://api.yourdomain.com/health | jq .
```

**Done when:** `api` + `worker` are `Up`, migrations report no pending changes, health is `Healthy`.

> ⚠️ If a release contains migrations, **take a backup first** (§6) so you can roll back (§11).

---

## 2. How to restart the API

```bash
docker compose restart api
# or recreate after a config/.env change:
docker compose up -d --no-deps api

docker compose logs -f --tail=100 api      # watch startup
```
Healthy startup ends with `Now listening on: http://[::]:8080` and `Application started`. If it **exits immediately**, it's almost always a missing/invalid secret — check the last log line (fail-fast validation names the missing variable).

---

## 3. How to restart the Worker

The Worker runs the Hangfire server (background sync, market engine, decay jobs).

```bash
docker compose restart worker
docker compose logs -f --tail=100 worker
```
Jobs are **idempotent and retried**, so a restart mid-job is safe. After restart, confirm jobs resume in the Hangfire dashboard (§4).

---

## 4. How to inspect Hangfire

Dashboard: `https://api.yourdomain.com/hangfire`

- **Access requires** an authenticated **Admin** account (§10) **and** HTTPS — it is not publicly reachable.
- Use it to see: **Recurring** jobs (sync tiers, inactivity decay), **Processing/Succeeded/Failed** queues, and to **requeue** a failed job.
- Quick DB-side check that the Hangfire schema/queues exist:
  ```bash
  docker compose exec postgres psql -U osu_stocks -d osu_stocks -c "\dn"   # expect a 'hangfire' schema
  ```
If the dashboard 404s/403s: confirm you're on HTTPS and your token's `role` is `Admin`.

---

## 5. How to verify health endpoints (smoke test)

```bash
# Liveness/readiness (no auth) — both should be 200 with "status":"Healthy"
curl -fsS https://api.yourdomain.com/health        | jq .
curl -fsS https://api.yourdomain.com/api/v1/health | jq .
```
`checks[]` must show `postgresql` and `redis` both `Healthy`. A `503` means a dependency is down — check `docker compose ps` and the relevant container's logs.

**Full authed smoke test (after a deploy):**
1. Browser → `https://api.yourdomain.com/api/v1/auth/login` → osu! consent → callback returns a token.
2. With `Authorization: Bearer <token>`: `GET /api/v1/auth/me` (expect your identity), `GET /api/v1/market`, `GET /api/v1/market/stocks`.
3. `POST /api/v1/trading/buy` a small quantity, then `POST /api/v1/trading/sell`; confirm `GET /api/v1/wallet` and `/portfolio` updated.
4. As admin: `GET /api/v1/admin/market-settings`, `GET /api/v1/admin/tracked-players`.

---

## 6. How to restore a PostgreSQL backup

Backups are custom-format `pg_dump` files (`.sql.gz`) in the `postgres_backups` volume, created by `scripts/pg-backup.sh` and restored with `scripts/pg-restore.sh` (uses `pg_restore`).

**Take a backup (also do this before any risky change):**
```bash
docker compose exec postgres /scripts/pg-backup.sh
docker compose exec postgres ls -lh /backups
```

**Restore (DESTRUCTIVE — drops & recreates the DB):**
```bash
# 1. Stop the apps so nothing writes mid-restore
docker compose stop api worker

# 2. Restore from a chosen backup file
docker compose exec postgres /scripts/pg-restore.sh /backups/osu_stocks_<TIMESTAMP>.sql.gz

# 3. Bring the apps back
docker compose start api worker
curl -fsS https://api.yourdomain.com/health | jq .
```
Off-host backup? Copy it into the volume first:
```bash
docker compose cp ./osu_stocks_<TIMESTAMP>.sql.gz postgres:/backups/
```
For restore-to-a-new-host and RTO/RPO, see [`DISASTER_RECOVERY.md`](DISASTER_RECOVERY.md).

---

## 7. How to rotate the JWT signing key

Rotating `Jwt__SigningKey` **invalidates all existing tokens** — every user must log in again. Schedule it during low traffic.

```bash
# 1. Generate a new key (>= 32 chars)
openssl rand -base64 48

# 2. Update Jwt__SigningKey in .env (and your secret store)

# 3. Recreate both services so they pick up the new value
docker compose up -d --no-deps api worker

# 4. Verify a fresh login works end-to-end (§5). Old tokens now return 401 → clients re-login.
```
There is no dual-key grace period; rotation is immediate.

---

## 8. How to rotate the osu! OAuth secret

```bash
# 1. In the osu! OAuth app, regenerate the client secret (https://osu.ppy.sh/home/account/edit#oauth)
# 2. Update OsuOAuth__ClientSecret in .env (Client ID and RedirectUri are unchanged)
# 3. Recreate the services that talk to osu!:
docker compose up -d --no-deps api worker
# 4. Verify a new login completes the code exchange (§5, step 1).
```
Existing user JWTs remain valid (they don't depend on the osu! secret). Only new logins and the Worker's osu! API calls use the new secret. Rotate immediately if the secret is ever exposed.

---

## 9. Useful day-to-day commands

```bash
docker compose ps                          # status of all services
docker compose logs -f --tail=200 api      # tail API logs
docker compose logs -f --tail=200 worker   # tail Worker logs
docker compose exec postgres psql -U osu_stocks -d osu_stocks   # DB shell
docker compose exec redis redis-cli         # Redis shell
docker stats                                # live resource usage
```

---

## 10. How to create / manage an Admin

No API grants admin (by design). Promote an existing user directly in the DB:

```bash
docker compose exec postgres psql -U osu_stocks -d osu_stocks \
  -c "UPDATE users SET role='Admin' WHERE osu_user_id=<your-osu-id>;"
```
The user must **log in again** to get a token carrying the new role. Demote with `role='User'`. Admin governs both the admin API endpoints and the Hangfire dashboard — grant sparingly.

---

## 11. Rollback procedure

Use when a deploy is bad. Two cases:

**A) Code-only change (no migration):**
```bash
git checkout v<previous-tag>          # or set the previous image tags in compose
docker compose up -d --build
curl -fsS https://api.yourdomain.com/health | jq .
```

**B) Change included a migration:**
1. `docker compose stop api worker`
2. Restore the **pre-deploy** backup (§6) — this reverts the schema + data to before the migration.
3. Redeploy the previous image/tag as in (A).
4. Smoke test (§5).

> This is why the checklist requires a backup **before** every migration deploy and that the previous image tag is known. Released images are tagged `vX.Y.Z` and `latest` in GHCR by `.github/workflows/release.yml`.

---

## Escalation quick reference

| Symptom | First check |
|---|---|
| `api` keeps exiting | Last log line — fail-fast names the missing/invalid env var (§2) |
| `/health` = 503 | `docker compose ps`; restart the unhealthy dependency |
| Logins fail at callback | `OsuOAuth__RedirectUri` matches the osu! app exactly; client secret current (§8) |
| Background jobs stalled | Hangfire dashboard (§4); restart `worker` (§3) |
| Trades return `MAINTENANCE_MODE` | `isMaintenanceMode` in `GET /admin/market-settings` — turn off via `PUT` |
| DB data loss / corruption | Restore latest backup (§6); see DISASTER_RECOVERY.md |
