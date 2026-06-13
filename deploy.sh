#!/usr/bin/env bash
#
# Manual, operator-triggered deploy to the production VPS over SSH. This is the
# deliberate alternative to CI auto-deploy: images are built and pushed to GHCR by
# the `v*`-tag release workflows, and a human runs this script to roll a chosen tag.
#
#   ./deploy.sh <backend-tag> [frontend-tag]
#   ./deploy.sh v1.2.0                # frontend tag defaults to the backend tag
#   ./deploy.sh v1.2.0 v0.9.0         # pin them independently
#
# Rollback is just re-running with an older tag (DB migrations are forward-only, so
# a rollback is image-only — do not roll back across a migration that changed schema).
#
# Config via environment (override as needed):
#   DEPLOY_HOST   ssh target            (default: deploy@ambiquality.org)
#   DEPLOY_DIR    remote deploy dir     (default: ~/ambiquality)
set -euo pipefail

BACKEND_TAG="${1:?usage: ./deploy.sh <backend-tag> [frontend-tag]}"
FRONTEND_TAG="${2:-$BACKEND_TAG}"
DEPLOY_HOST="${DEPLOY_HOST:-deploy@ambiquality.org}"
DEPLOY_DIR="${DEPLOY_DIR:-ambiquality}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$REPO_ROOT"

echo "▶ Deploying backend=$BACKEND_TAG frontend=$FRONTEND_TAG to $DEPLOY_HOST:$DEPLOY_DIR"

# 1. Sync the deploy-time files the compose bind-mounts (NOT the .env — that is
#    created once on the server and never leaves it). --delete keeps the remote
#    conf/ in lockstep with the repo so stale Caddy/init files can't linger.
echo "▶ Syncing deploy files…"
rsync -az --delete \
	--include='compose.ghcr.yml' \
	--include='init-databases.sh' \
	--include='init-databases.sql.tpl' \
	--include='conf/' --include='conf/***' \
	--exclude='*' \
	./ "$DEPLOY_HOST:$DEPLOY_DIR/"

# 2. On the host: use the production Caddyfile, pull the pinned images, roll the
#    stack, and prune old images. `up -d` re-runs the one-shot migrators idempotently.
echo "▶ Pulling images and rolling the stack…"
ssh "$DEPLOY_HOST" "
	set -euo pipefail
	cd '$DEPLOY_DIR'
	cp conf/Caddyfile.production conf/Caddyfile
	export TAG='$BACKEND_TAG' FRONTEND_TAG='$FRONTEND_TAG'
	podman compose -f compose.ghcr.yml pull
	podman compose -f compose.ghcr.yml up -d --remove-orphans
	podman image prune -f >/dev/null
"

# 3. Smoke-check through the real ingress. There is no /health endpoint; the
#    anonymous, always-present /v1/properties read is a cheap liveness probe that
#    exercises Caddy → public-api → Postgres end to end.
echo "▶ Waiting for the API to answer through Caddy…"
for i in $(seq 1 30); do
	if curl -fsS --max-time 5 "https://api.ambiquality.org/public/v1/properties" >/dev/null 2>&1; then
		echo "✓ Deploy live: api=$BACKEND_TAG, frontend=$FRONTEND_TAG"
		exit 0
	fi
	sleep 3
done

echo "✗ Health check did not pass within ~90s — inspect with:" >&2
echo "    ssh $DEPLOY_HOST 'cd $DEPLOY_DIR && podman compose -f compose.ghcr.yml ps && podman compose -f compose.ghcr.yml logs --tail=50'" >&2
exit 1
