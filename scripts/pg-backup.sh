#!/usr/bin/env bash
# PostgreSQL backup script for OsuStocks.
# Intended to run inside the postgres container or via docker compose exec.
#
# Usage (from project root):
#   docker compose exec postgres /scripts/pg-backup.sh
#
# Or via cron on the host:
#   docker compose exec -T postgres /scripts/pg-backup.sh

set -euo pipefail

# --- Configuration (override via environment) ---
BACKUP_DIR="${BACKUP_DIR:-/backups}"
PGUSER="${PGUSER:-osu_stocks}"
PGDATABASE="${PGDATABASE:-osu_stocks}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
TIMESTAMP="$(date -u +%Y%m%d_%H%M%S)"
BACKUP_FILE="${BACKUP_DIR}/osu_stocks_${TIMESTAMP}.sql.gz"

# --- Ensure backup directory exists ---
mkdir -p "${BACKUP_DIR}"

# --- Create backup ---
echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Starting backup of ${PGDATABASE}..."

pg_dump \
  --username="${PGUSER}" \
  --dbname="${PGDATABASE}" \
  --format=custom \
  --compress=6 \
  --file="${BACKUP_FILE}"

BACKUP_SIZE="$(du -h "${BACKUP_FILE}" | cut -f1)"
echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Backup complete: ${BACKUP_FILE} (${BACKUP_SIZE})"

# --- Prune old backups ---
PRUNED=0
if [ "${RETENTION_DAYS}" -gt 0 ]; then
  while IFS= read -r old_file; do
    rm -f "${old_file}"
    PRUNED=$((PRUNED + 1))
  done < <(find "${BACKUP_DIR}" -name "osu_stocks_*.sql.gz" -type f -mtime "+${RETENTION_DAYS}" 2>/dev/null || true)
fi

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Pruned ${PRUNED} backup(s) older than ${RETENTION_DAYS} days."
echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Backup job finished."
