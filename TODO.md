# TODO

Running backlog of outstanding work, read at the start of every session as a
complement to the session handoffs in `thoughts/shared/handoffs/`. Handoffs
capture *what happened*; this file captures *what still needs to happen*.

**Maintenance rule:** when an item is finished, **delete it** — do not check it
off and leave it. The file should only ever describe pending work, so it stays
small and cheap to load into context.

---

## API testing using Podman & Newman

The `postman/collections/ambiquality-api` suite has a request for every endpoint
of Auth, Evidence, Ingestion and Public (75 requests / 113 assertions, runs green
via `cd postman && npm install && npm test` against a fresh `./dev.sh up-d`
stack). It covers the F10 `POST /ingestion/measurements` (happy + 401 bad-key +
422 out-of-range) and a real register → confirm → login → change-email E2E that
pulls confirmation links from Mailpit. The git-native YAML under
`postman/collections/` is now version-controlled (committed 88305fc).

**The repo YAML is the source of truth** — it is more likely to match the
committed app code than the Postman cloud collection. Sync flows repo → cloud,
not the other way. Run-order fixes (Confirm Email after Register; the three
Ingest requests after Create Sensor, before Update Status flips to maintenance)
are already baked into the repo YAML and the newman build order.

**Still pending:**
- **Push repo → cloud (optional).** If the cloud collection is kept around, sync
  it FROM the repo so it stops drifting. Not required for `npm test`, which runs
  entirely off the repo YAML via `build-collection.mjs`.

## Testing & thesis chapter (plan: thoughts/shared/plans/PLAN-testing-and-thesis-chapter.md)

All four phases done 2026-06-12 (backend branch `test/newman-suite-refresh`;
chapter committed in MrLogEN/BP main as 3fb69a1, unpushed). Remaining:
- **Verify first CI run** of tests.yml after the branch is pushed (docker
  compose build of the full dev profile on a GH runner is unproven).
- **Push MrLogEN/BP main** (3 commits ahead) via HTTPS + gh creds (SSH auth
  unavailable in sessions).
- **Phase 4 — thesis** chapter `testovani.tex` in /home/vilem/FIS/BP (Czech,
  ≤ 5 pages); structure already in the plan file. Current numbers: xUnit 577/577,
  newman 75 req / 113 assertions.


## Docs — hosted documentation site (optional)

In-repo architecture docs now exist: `docs/adr/0001-…` (ADR) and `docs/er/` (ER diagrams for
all three schemas), linked from the root README; OpenAPI/Scalar covers the API surface. Still
open if desired: a **hosted** documentation site (out of scope for the docs plan), and keeping
the separate ArchiMate repo's higher-level models consistent with ADR 0001.
