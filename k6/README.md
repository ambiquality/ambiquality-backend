# k6 performance tests

Performance/stress tests verifying the thesis NFRs against a locally running
stack (`./dev.sh up-d`). CI may run a low-rate smoke, but **authoritative numbers
come from a local run on documented hardware** — shared CI runners are too noisy.

| Script | NFR under test | Pass/fail |
|--------|----------------|-----------|
| `public-read.js` | Read API p95 < 1 s, p99 < 3 s, pages ≤ 100 records, ≥ 50 concurrent | thresholds |
| `ingestion-write.js` | Ingestion ≥ 100 measurements/s sustained (202 accept path) | thresholds |
| `stress.js` | none — finds the saturation point of the read API | exploratory |

## Usage

```bash
./dev.sh up-d                 # start the stack
./k6/seed.sh                  # create user/building/room/sensor → k6/seed.json
mkdir -p k6/results

k6 run --summary-export k6/results/public-read.json k6/public-read.js
k6 run --summary-export k6/results/ingestion.json   k6/ingestion-write.js
k6 run --summary-export k6/results/stress.json      k6/stress.js
```

Knobs (env vars): `BASE_URL`, `VUS`/`DURATION` (public-read), `RATE`/`DURATION`
(ingestion-write). A CI smoke is just lower knobs, e.g.
`k6 run -e VUS=5 -e DURATION=30s k6/public-read.js`.

Notes:
- `seed.json` is per-stack state (contains a sensor API key) — gitignored;
  re-run `seed.sh` after a stack reset.
- `ingestion-write.js` sends one reading per request, so the request rate equals
  the measurement rate. The 202 means *durably enqueued*; the worker materializes
  rows asynchronously (verify drain via the Public API observation count if
  needed).
- For thesis results, record the run environment (CPU, RAM, storage, podman
  version) alongside the exported summary JSON.
