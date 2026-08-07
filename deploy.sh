#!/usr/bin/env bash
#
# Manual, operator-triggered deploy to the production VPS over SSH. This is the
# deliberate alternative to CI auto-deploy: images are built and pushed to GHCR by
# the `v*`-tag release workflows, and a human runs this script to roll a chosen tag.
#
#   ./deploy.sh <backend-tag> [frontend-tag]
#   ./deploy.sh v0.2.1                # frontend tag defaults to the backend tag
#   ./deploy.sh v0.2.1 v0.1.0         # pin them independently
#
# Git tags are `vX.Y.Z`, but the release workflow's metadata-action strips the
# leading `v`, so the GHCR image tags are `X.Y.Z`. This script accepts either form
# and normalizes it, so `v0.2.1` and `0.2.1` both resolve to the :0.2.1 image.
#
# Rollback is just re-running with an older tag (DB migrations are forward-only, so
# a rollback is image-only — do not roll back across a migration that changed schema).
#
# Config via environment (override as needed):
#   DEPLOY_HOST   ssh target            (default: ambiquality@ambiquality.org)
#   DEPLOY_DIR    remote deploy dir     (default: ~/ambiquality)
set -euo pipefail

# Accept v-prefixed or bare versions; the image tags are bare (see above).
BACKEND_TAG="${1:?usage: ./deploy.sh <backend-tag> [frontend-tag]}"
FRONTEND_TAG="${2:-$BACKEND_TAG}"
BACKEND_TAG="${BACKEND_TAG#v}"
FRONTEND_TAG="${FRONTEND_TAG#v}"
DEPLOY_HOST="${DEPLOY_HOST:-ambiquality@ambiquality.org}"
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
	--include='compose.monitoring.yml' \
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
	podman compose -f compose.ghcr.yml -f compose.monitoring.yml pull
	podman compose -f compose.ghcr.yml -f compose.monitoring.yml up -d --remove-orphans
	podman image prune -f >/dev/null
"

# 3. Smoke-check through the real ingress. There is no /health endpoint; the
#    anonymous, always-present /v1/properties read is a cheap liveness probe that
#    exercises Caddy → public-api → Postgres end to end.
echo "▶ Waiting for the API to answer through Caddy…"
for i in $(seq 1 30); do
	if curl -fsS --max-time 5 "https://api.ambiquality.org/public/v1/properties" >/dev/null 2>&1; then
		echo "✓ Deploy live: api=$BACKEND_TAG, frontend=$FRONTEND_TAG"
		break
	fi
	if [ "$i" -eq 30 ]; then
		echo "✗ Health check did not pass within ~90s — inspect with:" >&2
		echo "    ssh $DEPLOY_HOST 'cd $DEPLOY_DIR && podman compose -f compose.ghcr.yml ps && podman compose -f compose.ghcr.yml logs --tail=50'" >&2
		exit 1
	fi
	sleep 3
done

# 4. Confirm the canonical open-data host dereferences (DNS + the data. Caddy
#    block + PUBLIC_API_BASE_IRI must all be in place). Non-fatal: the deploy is
#    already live above; a failure here just means the data. ingress/DNS or the
#    base-IRI env still needs wiring, so warn rather than fail the roll.
echo "▶ Checking the canonical open-data host (data.ambiquality.org)…"
if curl -fsS --max-time 5 "https://data.ambiquality.org/v1/properties" >/dev/null 2>&1; then
	echo "✓ Identifier host live: https://data.ambiquality.org/v1/…"
else
	echo "⚠ data.ambiquality.org/v1/properties did not answer — published IRIs will not" >&2
	echo "  dereference. Check the data. DNS record, the Caddyfile.production data. block," >&2
	echo "  and PUBLIC_API_BASE_IRI=https://data.ambiquality.org/v1 in the server .env." >&2
fi
exit 0
