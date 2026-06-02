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
of Auth, Evidence, Ingestion and Public (60 requests / 86 assertions, runs green
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
- **Broaden unhappy paths.** Only ingestion and the confirm flow exercise
  failures today; the rest are happy-path only. Add 401 on protected
  Evidence/Account routes, 404 / 422 on bad payloads, etc.


## Docs

The API docs is handled by the openapi and a frontend for the openapi docs.
However, there is not docs that explain the low level architeture decisions. There is only in code docs, no web.  

The docs should include:

- ER diagrams
- architecture overview (handled by the archimate repo)

That's only a rough plan - we need to chat about it 

## Evidence HTTP methods

There are HTTP `DELETE` methods in the evidence, the semantic meanings is wrong for the endpoints. Nothing get's actually deleted, only the status changes as every change is versioned. The question is if it should be left as is or corrected with `PUT`/`PATCH`/`POST`. 
