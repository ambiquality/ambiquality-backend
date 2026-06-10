#!/bin/sh
# Backup scheduler: run once immediately (so a fresh deployment has a backup right
# away), then every BACKUP_INTERVAL_SECONDS. The default 24 h interval is the RPO
# ceiling SPO-04 allows. A failed run logs and the loop keeps going — the next tick
# retries rather than killing the container.
set -eu

: "${BACKUP_INTERVAL_SECONDS:=86400}"

echo "[backup] scheduler started (interval ${BACKUP_INTERVAL_SECONDS}s)"
while true; do
  if ! /usr/local/bin/backup.sh; then
    echo "[backup] ERROR: backup run failed; retrying in ${BACKUP_INTERVAL_SECONDS}s" >&2
  fi
  sleep "$BACKUP_INTERVAL_SECONDS"
done
