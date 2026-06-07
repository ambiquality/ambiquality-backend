#!/bin/sh
set -e

echo "Seeding development databases..."

psql -h postgres -U postgres -d auth    -f /seed-dev-auth.sql
psql -h postgres -U postgres -d evidence -f /seed-dev-evidence.sql
psql -h postgres -U postgres -d ieq     -f /seed-dev-ieq.sql

echo "Seed complete."
