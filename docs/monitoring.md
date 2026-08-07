# Monitoring (Four Golden Signals)

The observability stack collects **latency, traffic, errors and saturation** for the
whole platform and exposes them as four Grafana dashboards. It runs as a second compose
overlay (`compose.monitoring.yml`) added to the app stack in **both** environments:

```bash
# dev — dev.sh already includes the overlay
./dev.sh up -d

# prod — deploy.sh includes it; or manually
podman compose -f compose.ghcr.yml -f compose.monitoring.yml up -d
```

## Components

| Component            | Role                                                        | Reaches |
|----------------------|-------------------------------------------------------------|---------|
| **Prometheus** (`:9090`) | scrape + storage (30d retention)                        | every `/metrics` below |
| **Grafana** (`:3000`)    | dashboards, provisioned from `conf/grafana`              | Prometheus only |
| **.NET services** (ports 9464–9469) | OpenTelemetry metrics (RED + runtime + business) via a dedicated HttpListener | — |
| **node-exporter** (`:9100`) | host CPU/mem/disk of the VPS | host `/proc`,`/sys` |
| **redis-exporter** (`:9121`) | Redis saturation (connections, memory) | `redis:6379` |
| **postgres-exporter** (`:9187`) | Postgres/TimescaleDB connections, locks | `postgres:5432` |

> Per-container CPU/memory for the six .NET services comes from their
> `dotnet.process.*` metrics (labeled with the scrape-job `service` label). cAdvisor was
> evaluated and dropped: under rootless Podman it cannot read sibling containers' cgroup
> data (only the machine `/` series appears).

### Metrics ports (internal only)

Each service serves `/metrics` on its own **internal** port via an OpenTelemetry
`HttpListener` — never the application's HTTP port, so `/metrics` can never be reached
through Caddy or the host.

| Service        | Port |
|----------------|------|
| auth-api       | 9464 |
| evidence-api   | 9465 |
| ingestion-api  | 9466 |
| public-api     | 9467 |
| ingestion-worker | 9468 |
| export-worker  | 9469 |

## Dashboards

| Dashboard | What it answers |
|-----------|-----------------|
| **Overview (10,000-ft)** | Total Active Users (5m/1h/24h), Global Error Rate (aggregated 5xx), Core Web Vitals (LCP/INP/TTFB/CLS bar gauges), page views, services up |
| **Service RED** (`$service`) | Rate (rps), Errors (5xx + %), Duration (p99/p95/p50/avg) per service — one dashboard, `service` dropdown |
| **Infrastructure USE** (`$service`) | CPU cores, memory, GC heap, thread-pool queue per service; host CPU/mem; Postgres/Redis saturation; ingestion queue depth + drain gap |
| **Ingestion Pipeline** | enqueue rate (the ≥100 meas/s NFR), batches by outcome, queue depth, drain lag, unacked, archives |

The `service` label is added by the Prometheus scrape config (one job, six targets), so
the RED/USE dashboards switch services with a single dropdown instead of per-service
graphs.

## Access (SSH port forwarding only)

Grafana and Prometheus are bound to `127.0.0.1` on the host and are **never** exposed on
the public ingress:

```bash
ssh -L 3000:127.0.0.1:3000 -L 9090:127.0.0.1:9090 ambiquality@ambiquality.org
# Grafana:    http://localhost:3000 (admin / GF_ADMIN_PASSWORD in the server .env)
# Prometheus: http://localhost:9090
```

## Core Web Vitals (RUM)

The SPA reports anonymized LCP/INP/TTFB/CLS on `pagehide` via `sendBeacon` to
Public.Api's `POST /telemetry/vitals` (deliberately excluded from the public OpenAPI
document). Those feed the `ambiquality.web_vitals.*` histograms. The release workflow
bakes `VITE_RUM_ENDPOINT` into the bundle; the endpoint is CORS-open like the rest of
Public.Api.

## Configuration

- `conf/prometheus/prometheus.yml` — scrape jobs (add targets here).
- `conf/grafana/` — datasource, dashboard provider, dashboard JSONs.
- Server `.env`: `GF_ADMIN_USER`, `GF_ADMIN_PASSWORD` (required), `PROMETHEUS_RETENTION`.

## Testing

- Backend: `dotnet test` — `Ambiquality.Observability.Tests` (rolling-window logic) and
  `Ambiquality.Public.Api.Tests.RumVitalsEndpointTests` (beacon endpoint behavior +
  OpenAPI exclusion).
- Smoke: `up -d` then `curl` each `:946x/metrics`, open `http://localhost:9090/targets`
  (all `up`), run `k6/` to see RED move.

## Notes / gotchas

- The OTel Prometheus exporter serves the legacy text format (`UnderscoreEscaping...`),
  but Prometheus v3 stores its metric names in the dotted UTF-8 form; the dashboards
  reference them via `{__name__="..."}` matchers (raw dotted names don't parse in PromQL).
- Counters (e.g. `ambiquality.ingestion.measurements_enqueued_total`) only appear once a
  value has been recorded after a service restart — empty panels simply mean no traffic.
