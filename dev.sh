#!/usr/bin/env bash
set -euo pipefail

COMPOSE="podman compose -f podman-compose.yml --profile development"

case "${1:-}" in
  up)
    $COMPOSE up
    ;;
  down)
    $COMPOSE down -v
    ;;
  *)
    echo "Usage: $0 [up|down]"
    exit 1
    ;;
esac
