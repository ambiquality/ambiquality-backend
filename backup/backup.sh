#!/bin/sh
# One backup run (SPO-04): logical-dump every platform database plus the cluster
# globals (roles, grants), then push the run off-site to an S3-compatible bucket.
# pg_dump's custom format is compressed and restorable per-database with pg_restore;
# globals.sql replays with psql before restoring the per-database dumps.
set -eu

: "${PGHOST:=postgres}"
: "${PGPORT:=5432}"
: "${PGUSER:=postgres}"
export PGHOST PGPORT PGUSER
: "${BACKUP_DIR:=/backups}"
: "${BACKUP_DATABASES:=auth evidence ieq}"
: "${BACKUP_RETENTION_DAYS:=7}"

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
target="$BACKUP_DIR/$stamp"
mkdir -p "$target"

echo "[backup] run $stamp starting (databases: $BACKUP_DATABASES)"

pg_dumpall --globals-only --no-password > "$target/globals.sql"
for db in $BACKUP_DATABASES; do
  pg_dump --format=custom --no-password --dbname="$db" --file="$target/$db.dump"
  echo "[backup] dumped $db ($(du -h "$target/$db.dump" | cut -f1))"
done

if [ -n "${BACKUP_S3_BUCKET:-}" ]; then
  # The bucket is the off-site copy SPO-04 requires — it must live on storage
  # independent from the postgres volume. Retention there is the bucket's
  # lifecycle policy; BACKUP_RETENTION_DAYS only prunes the local staging volume.
  aws s3 cp "$target" \
    "s3://$BACKUP_S3_BUCKET/${BACKUP_S3_PREFIX:-postgres}/$stamp/" \
    --recursive --only-show-errors \
    ${BACKUP_S3_ENDPOINT:+--endpoint-url "$BACKUP_S3_ENDPOINT"}
  echo "[backup] uploaded run $stamp to s3://$BACKUP_S3_BUCKET/${BACKUP_S3_PREFIX:-postgres}/$stamp/"
else
  echo "[backup] WARNING: BACKUP_S3_BUCKET unset — run kept on the local volume only," \
    "which does NOT satisfy the off-site requirement (SPO-04). Configure S3 for production." >&2
fi

find "$BACKUP_DIR" -mindepth 1 -maxdepth 1 -type d -mtime +"$BACKUP_RETENTION_DAYS" -exec rm -rf {} +

echo "[backup] run $stamp complete"
