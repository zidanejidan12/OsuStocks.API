#!/usr/bin/env bash
# PostgreSQL restore script for OsuStocks.
#
# Usage (from project root):
#   docker compose exec postgres /scripts/pg-restore.sh /backups/osu_stocks_20260606_030000.sql.gz
#
# WARNING: This drops and recreates the target database.

set -euo pipefail

BACKUP_FILE="${1:?Usage: pg-restore.sh <backup-file>}"
PGUSER="${PGUSER:-osu_stocks}"
PGDATABASE="${PGDATABASE:-osu_stocks}"

if [ ! -f "${BACKUP_FILE}" ]; then
  echo "ERROR: Backup file not found: ${BACKUP_FILE}"
  exit 1
fi

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Restoring ${PGDATABASE} from ${BACKUP_FILE}..."
echo "WARNING: This will drop and recreate the database. Press Ctrl+C within 5 seconds to abort."
sleep 5

# Terminate existing connections
psql --username="${PGUSER}" --dbname=postgres -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '${PGDATABASE}' AND pid <> pg_backend_pid();" \
  2>/dev/null || true

# Drop and recreate
dropdb --username="${PGUSER}" --if-exists "${PGDATABASE}"
createdb --username="${PGUSER}" "${PGDATABASE}"

# Restore
pg_restore \
  --username="${PGUSER}" \
  --dbname="${PGDATABASE}" \
  --no-owner \
  --no-privileges \
  "${BACKUP_FILE}"

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Restore complete."
