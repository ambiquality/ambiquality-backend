#!/bin/sh
set -eu
sed \
  -e "s|\${AUTH_API_DB_PASSWORD}|${AUTH_API_DB_PASSWORD}|g" \
  -e "s|\${EVIDENCE_API_DB_PASSWORD}|${EVIDENCE_API_DB_PASSWORD}|g" \
  -e "s|\${INGESTION_API_DB_PASSWORD}|${INGESTION_API_DB_PASSWORD}|g" \
  -e "s|\${PUBLIC_API_DB_PASSWORD}|${PUBLIC_API_DB_PASSWORD}|g" \
  /docker-entrypoint-initdb.d/init-databases.sql.tpl \
  | psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB"
