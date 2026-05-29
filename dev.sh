#!/usr/bin/env bash
set -euo pipefail

COMPOSE="podman compose -f podman-compose.yml --profile development"

case "${1:-}" in
  up)
    $COMPOSE up
    ;;
  up-d)
    $COMPOSE up -d
    ;;
  stop)
    $COMPOSE stop
    ;;
  down)
    $COMPOSE down -v
    ;;
  *)
    echo "Usage: $0 [up|up-d|stop|down]"
    echo "  up     start in foreground"
    echo "  up-d   start detached"
    echo "  stop   stop containers, keep volumes"
    echo "  down   stop and remove volumes"
    exit 1
    ;;
esac
