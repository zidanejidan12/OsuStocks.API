# Disaster Recovery

## Backup Strategy

### Overview

PostgreSQL is the sole persistent store. Redis is used only for ephemeral OAuth tokens and can be rebuilt without data loss. Backups target the PostgreSQL `osu_stocks` database only.

### Backup Method

Backups use `pg_dump` in custom format with compression, executed inside the `postgres` container via the `scripts/pg-backup.sh` script.

Each backup produces a file named `osu_stocks_YYYYMMDD_HHMMSS.sql.gz` in the `/backups` volume.

### Scheduling Backups

#### Option 1: Host cron (recommended)

Add a cron entry on the Docker host:

```bash
# Daily backup at 02:00 UTC
0 2 * * * cd /path/to/project && docker compose exec -T postgres /scripts/pg-backup.sh >> /var/log/osu-stocks-backup.log 2>&1
```

#### Option 2: Manual backup

```bash
docker compose exec postgres /scripts/pg-backup.sh
```

#### Option 3: Ad-hoc backup before maintenance

```bash
docker compose exec -T postgres /scripts/pg-backup.sh
docker compose down
# perform maintenance
docker compose up -d
```

### Retention Policy

The backup script automatically prunes backups older than the retention period.

| Parameter | Default | Override |
|-----------|---------|----------|
| `RETENTION_DAYS` | 30 | Set as environment variable |
| `BACKUP_DIR` | `/backups` | Set as environment variable |

Example with custom retention:

```bash
docker compose exec -e RETENTION_DAYS=14 postgres /scripts/pg-backup.sh
```

### Backup Storage

By default, backups are stored in the `postgres_backups` Docker volume. For production, copy backups to an off-host location:

```bash
# Copy latest backup from container to host
docker compose cp postgres:/backups/. ./local-backups/

# Sync to remote storage (example with rclone)
rclone sync ./local-backups/ remote:osu-stocks-backups/
```

### Verifying Backups

List available backups:

```bash
docker compose exec postgres ls -lht /backups/
```

Verify a backup file is readable:

```bash
docker compose exec postgres pg_restore --list /backups/osu_stocks_20260606_020000.sql.gz
```

---

## Restore Procedure

### Full Restore (same host)

**WARNING**: This drops and recreates the database. All current data is lost.

1. Stop the API and Worker to prevent writes:

```bash
docker compose stop api worker
```

2. Run the restore script with the backup file:

```bash
docker compose exec postgres /scripts/pg-restore.sh /backups/osu_stocks_20260606_020000.sql.gz
```

3. Restart services:

```bash
docker compose start api worker
```

4. Verify health:

```bash
curl http://localhost/api/v1/health
```

### Restore to a New Host

1. Copy the backup file to the new host.

2. Start only the database:

```bash
docker compose up -d postgres
```

3. Copy the backup into the container:

```bash
docker compose cp ./osu_stocks_20260606_020000.sql.gz postgres:/backups/
```

4. Run the restore:

```bash
docker compose exec postgres /scripts/pg-restore.sh /backups/osu_stocks_20260606_020000.sql.gz
```

5. Start remaining services:

```bash
docker compose up -d
```

### Point-in-Time Recovery

The current backup strategy uses logical backups (`pg_dump`). Point-in-time recovery (PITR) using WAL archiving is not configured. For PITR capability, consider:

- Enabling WAL archiving in the PostgreSQL configuration.
- Using a managed PostgreSQL service with built-in PITR.

---

## What Is Backed Up

| Data | Backed Up | Notes |
|------|-----------|-------|
| Users, wallets, portfolios | Yes | Core user data |
| Trades, holdings, transactions | Yes | Financial records |
| Tracked players, stocks, snapshots | Yes | Market data |
| Market events, price history | Yes | Historical data |
| Market settings | Yes | Admin configuration |
| Hangfire job state | Yes | Included in database |
| Redis cache (OAuth tokens) | No | Ephemeral; regenerated on next login |

## What Is NOT Backed Up

- Redis data (stateless cache, no backup needed)
- Application configuration (stored in source control and `.env`)
- Docker images (rebuilt from source or pulled from registry)
- TLS certificates (managed separately)

---

## Recovery Time Objectives

| Scenario | Expected Recovery Time |
|----------|----------------------|
| Restore from local backup | 5-15 minutes |
| Restore to new host | 15-30 minutes |
| Full rebuild from source + backup | 30-60 minutes |

These estimates assume small-to-medium data volumes (< 1 GB database).

---

## Runbook: Complete Disaster Recovery

If the entire host is lost:

1. Provision a new server with Docker and Docker Compose.
2. Clone the repository.
3. Copy `.env` from secure storage (never committed to git).
4. Copy the most recent backup file.
5. Run: `docker compose up -d postgres` and wait for healthy.
6. Copy backup into container and run `pg-restore.sh`.
7. Run: `docker compose up -d`.
8. Verify: `curl http://localhost/api/v1/health`.
9. Update DNS if the host IP changed.
10. Re-configure TLS certificates if applicable.
