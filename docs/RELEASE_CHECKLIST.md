# Production Release Checklist — v0.0.1

Work top to bottom. Each item lists **how** to do it and the **done-when** acceptance check. Don't tick a box until its acceptance check passes.

> Companion docs: [`ENVIRONMENT_VARIABLES.md`](ENVIRONMENT_VARIABLES.md) · [`OPERATIONS_RUNBOOK.md`](OPERATIONS_RUNBOOK.md) · [`DEPLOYMENT.md`](DEPLOYMENT.md) · [`DISASTER_RECOVERY.md`](DISASTER_RECOVERY.md)

Legend: `api` `worker` `postgres` `redis` `nginx` are the `docker-compose.yml` service names.

---

## Infrastructure

- [ ] **Domain purchased**
  - Register the apex/app domain (e.g. `yourdomain.com`) with a registrar.
  - **Done when:** `whois yourdomain.com` shows your ownership and the domain is active.

- [ ] **DNS configured**
  - Create `A`/`AAAA` records: `api.yourdomain.com` and `app.yourdomain.com` → the VPS public IP.
  - **Done when:** `dig +short api.yourdomain.com` returns the VPS IP from multiple resolvers (allow for propagation).

- [ ] **VPS provisioned**
  - Provision a Linux host (≥2 vCPU / 4 GB RAM recommended). Install Docker Engine + Compose plugin. Harden: non-root deploy user, SSH keys only, firewall allowing **80/443** (and 22 from admin IPs).
  - **Done when:** `docker compose version` works on the host and ports 80/443 are reachable.

- [ ] **PostgreSQL provisioned**
  - Use the `postgres` service in `docker-compose.yml` (PostgreSQL 16). Set a strong `POSTGRES_PASSWORD` in `.env`. Confirm the `postgres_data` volume is on durable storage.
  - Apply schema migrations (the app does **not** auto-migrate) — see Runbook §1.
  - **Done when:** `docker compose exec postgres psql -U osu_stocks -d osu_stocks -c "\dt"` lists the 13 tables incl. `__EFMigrationsHistory`.

- [ ] **Redis provisioned**
  - Use the `redis` service (Redis 7). Optionally set a password and reflect it in `ConnectionStrings__Redis`.
  - **Done when:** `docker compose exec redis redis-cli ping` → `PONG`.

- [ ] **SSL certificate active**
  - Obtain a TLS cert (Let's Encrypt/certbot) for `api.yourdomain.com`. Add an HTTPS server block to `nginx/nginx.conf` and forward `X-Forwarded-Proto: https` to `api`.
  - **Done when:** `curl -I https://api.yourdomain.com/health` returns `200` over a valid (non-self-signed) certificate, and HTTP redirects to HTTPS.

## Configuration

- [ ] **Environment variables configured**
  - Create `.env` from `.env.example`; fill every **required** variable per [`ENVIRONMENT_VARIABLES.md`](ENVIRONMENT_VARIABLES.md). Generate `Jwt__SigningKey` with `openssl rand -base64 48`. Use a strong `POSTGRES_PASSWORD`.
  - **Done when:** `docker compose up -d` starts `api` and `worker` and they **stay up** (no fail-fast secret-validation exit). `docker compose ps` shows both `Up`.

- [ ] **osu! OAuth production callback URL configured**
  - In the osu! OAuth app (https://osu.ppy.sh/home/account/edit#oauth), set the callback to exactly `https://api.yourdomain.com/api/v1/auth/callback`. Set the same value in `OsuOAuth__RedirectUri`.
  - **Done when:** opening `https://api.yourdomain.com/api/v1/auth/login` in a browser reaches the osu! consent page, and after authorizing, the callback returns `{ accessToken, expiresAt }`.

## Resilience

- [ ] **Backups configured**
  - Verify `scripts/pg-backup.sh` runs and schedule it (host cron, 02:00 UTC daily). Copy backups **off-host** (object storage). Set `RETENTION_DAYS`.
  - **Done when:** a backup file `osu_stocks_<ts>.sql.gz` exists in the `postgres_backups` volume, **and** a copy exists off-host. (Runbook §6.)

- [ ] **Monitoring configured**
  - Add an uptime check on `GET https://api.yourdomain.com/health` (expect `200` + `"status":"Healthy"`). Ship `api`/`worker` container logs somewhere durable. Alert on: health != Healthy, container restarts, disk on the DB volume.
  - **Done when:** the uptime check is green and an intentional `docker compose stop api` triggers an alert.

- [ ] **Admin account created**
  - There is no self-serve admin endpoint. Log in once via osu! to create your `User` row, then promote it (Runbook §10): `UPDATE users SET role='Admin' WHERE osu_user_id=<your-osu-id>;`
  - **Done when:** `GET /api/v1/auth/me` returns `"role":"Admin"` for that account, and `/hangfire` is reachable for it over HTTPS.

## Verification

- [ ] **Smoke test completed**
  - Run the end-to-end smoke test in Runbook §5: health (×2), real osu! login → `/auth/me`, `/market`, `/market/stocks`, a small buy then sell, `/portfolio`, `/wallet`, and (as admin) `/admin/market-settings` + `/admin/tracked-players`.
  - **Done when:** every step returns the expected `2xx` and the wallet/portfolio reflect the test trades. Record the run date + git tag.

- [ ] **Rollback procedure documented**
  - Confirm the rollback steps (Runbook §11) are accurate for this deployment: pin to the previous image tag, redeploy, and (if a migration shipped) restore the pre-deploy DB backup.
  - **Done when:** the previous image tag is known/available in GHCR and a pre-deploy backup exists, so rollback is a known, tested sequence.

---

## Sign-off

| Item | Owner | Date | Notes |
|---|---|---|---|
| Release tag (`vX.Y.Z`) | | | |
| Smoke test passed | | | |
| Backup + off-host copy verified | | | |
| Rollback target image tag | | | |
| Go / No-go | | | |
