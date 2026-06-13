# Deployment

How the Ambiquality stack is deployed to production. The design goal is a **simple,
single-VPS, operator-triggered** deploy: GitHub builds versioned images, and a human
runs one script to roll a chosen version. There is no CI auto-deploy.

## Topology

One Hetzner VPS (2 vCPU / 4 GB) runs the whole stack under **rootless Podman**, with a
Hetzner **block volume** holding all persistent data and a Hetzner **S3 bucket** for
off-site backups and data-export archives.

```
                          VPS  (rootless Podman, compose.ghcr.yml)
  Internet ──TLS──►  Caddy  (only published container, :80/:443)
   :443/:80           ├─ ambiquality.org, www  → frontend (static SPA)
                      └─ api.ambiquality.org   → /auth /evidence /ingestion /public
                          ┌──── internal networks (db, public) ────┐
                          │ auth-api evidence-api ingestion-api     │
                          │ ingestion-worker export-worker public-api│
                          │ postgres(TimescaleDB) redis postgres-backup│
                          └──────────────────────────────────────────┘
                          named volumes → /mnt/data (block volume)
                          daily pg_dump  → Hetzner S3 (off-site, RPO ≤ 24 h)
```

| Host | Record | Serves |
|------|--------|--------|
| `ambiquality.org` + `www` | A → VPS IP (Cloudflare, **DNS-only / grey cloud**) | frontend SPA |
| `api.ambiquality.org` | A → VPS IP (**DNS-only / grey cloud**) | backend API |

DNS-only (not Cloudflare-proxied) is required so Caddy's Let's Encrypt ACME challenge
reaches the origin directly.

## Building images (CI)

Pushing a semver tag triggers a GHCR release — **no ordinary push to `main` publishes
anything.**

| Repo | Tag | Builds |
|------|-----|--------|
| `ambiquality-backend` | `v*` | 10 images: each API, its migrator, the workers, `postgres-backup` |
| `ambiquality-frontend` | `v*` | `ghcr.io/ambiquality/frontend` (SPA with prod `VITE_*` baked in) |

```bash
# backend release
git -C ambiquality-backend tag v1.0.0 && git -C ambiquality-backend push origin v1.0.0
# frontend release
git -C ambiquality-frontend tag v1.0.0 && git -C ambiquality-frontend push origin v1.0.0
```

The frontend's API origins are inlined at build time (Vite), so a domain change needs a
fresh frontend tag — they live in `ambiquality-frontend/.github/workflows/release.yml`.

## Deploying

From a checkout of `ambiquality-backend` on your machine:

```bash
./deploy.sh v1.0.0            # frontend tag defaults to the backend tag
./deploy.sh v1.0.0 v0.9.0     # pin backend and frontend independently
```

`deploy.sh` rsyncs the deploy-time files (`compose.ghcr.yml`, `conf/`, the init scripts —
**never** the `.env`), then over SSH copies `Caddyfile.production` into place, pulls the
pinned images, `up -d --remove-orphans`, and smoke-checks `https://api.ambiquality.org/public/v1/properties`.

**Rollback** = re-run with an older tag. Migrations are forward-only, so do not roll back
across a schema change (image-only rollback is safe).

Override the target with env vars: `DEPLOY_HOST=deploy@1.2.3.4 DEPLOY_DIR=ambiquality ./deploy.sh v1.0.0`.

## One-time VPS setup

You already did: no root login, no password (key-only) SSH. Remaining, as the non-root
deploy user:

1. **Block volume** — format (if fresh) and mount persistently so all container data lives
   on it:
   ```bash
   sudo mkfs.ext4 -F /dev/disk/by-id/scsi-0HC_Volume_<id>     # only if unformatted
   sudo mkdir -p /mnt/data && sudo chown $USER:$USER /mnt/data
   # /etc/fstab:  /dev/disk/by-id/scsi-0HC_Volume_<id>  /mnt/data  ext4  discard,nofail,defaults  0 0
   sudo mount /mnt/data
   ```

2. **Firewall** — only SSH + web (mirror this in the Hetzner Cloud Firewall too):
   ```bash
   sudo ufw default deny incoming && sudo ufw default allow outgoing
   sudo ufw allow 22/tcp && sudo ufw allow 80/tcp && sudo ufw allow 443/tcp && sudo ufw enable
   ```

3. **Rootless Podman**, with all storage on the block volume so the small root disk stays
   free and every named volume (postgres-data, redis-data, backup-data, export-data,
   caddy_data) lands on `/mnt/data`:
   ```bash
   sudo apt install -y podman podman-compose
   sudo loginctl enable-linger "$USER"            # keep containers up after logout
   mkdir -p ~/.config/containers
   cat > ~/.config/containers/storage.conf <<'EOF'
   [storage]
   driver = "overlay"
   graphroot = "/mnt/data/containers"
   EOF
   ```

4. **GHCR login** — images are private; create a GitHub PAT with `read:packages`:
   ```bash
   echo "$GHCR_PAT" | podman login ghcr.io -u <github-user> --password-stdin
   ```

5. **Deploy dir + env** — create `~/ambiquality` and the server `.env` (deploy.sh syncs the
   rest):
   ```bash
   mkdir -p ~/ambiquality
   # copy .env.production.example from the repo, fill it in, lock it down:
   cp .env.production.example ~/ambiquality/.env && chmod 600 ~/ambiquality/.env
   ```
   Fill in fresh secrets (`openssl rand -hex 32` for `JWT_SECRET`), the SMTP relay, and the
   Hetzner S3 credentials for exports and backups.

6. **Unattended security updates**: `sudo apt install -y unattended-upgrades`.

7. **DNS** — add the `api` A-record in Cloudflare → VPS IP, **grey cloud (DNS-only)**. The
   apex and `www` already resolve.

## Backups & restore

`postgres-backup` runs `pg_dump` of `auth`, `evidence`, `ieq` (+ cluster globals) on
`BACKUP_INTERVAL_SECONDS`, keeps `BACKUP_RETENTION_DAYS` locally on the `backup-data`
volume, and — when the `BACKUP_S3_*` vars are set — copies each run to the Hetzner bucket
(off-site, on storage independent from the DB volume). See `backup/backup.sh` for the
restore procedure.

## Notes

- **First boot only**: the per-service DB passwords are baked into the Postgres roles when
  `init-databases.sh` runs against an empty data volume. Changing them later needs an
  `ALTER ROLE`, not just an `.env` edit.
- Redis runs AOF `appendfsync always` (durable ingestion queue). On network-backed block
  storage this is fsync-latency-sensitive but well within the 100 msmt/s target.
- Only Caddy publishes ports; Postgres and Redis are reachable only on the internal
  Podman networks.
