#!/usr/bin/env bash
set -euo pipefail

podman compose -f podman-compose.yml -f compose.monitoring.yml --profile development up --build
