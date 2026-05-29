# TODO

Running backlog of outstanding work, read at the start of every session as a
complement to the session handoffs in `thoughts/shared/handoffs/`. Handoffs
capture *what happened*; this file captures *what still needs to happen*.

**Maintenance rule:** when an item is finished, **delete it** — do not check it
off and leave it. The file should only ever describe pending work, so it stays
small and cheap to load into context.

---

## API testing using Podman & Newman

The `postman/collections/ambiquality-api` suite now has a request for every
endpoint of Auth, Evidence and Ingestion — including the F10
`POST /ingestion/measurements` (happy + 401 bad-key + 422 out-of-range) and a
real register → confirm → login → change-email E2E that pulls confirmation
links from Mailpit. Edits live in the Postman **cloud** collection.

**Still pending:**
- **Reorder + sync (do first).** In the Postman Runner move *Confirm Email* to
  run after Register (before Login) and the three *Ingest Measurement* requests
  to run after Create Sensor (before Update Status flips the sensor to
  `maintenance`). Then sync cloud → repo YAML files and commit — the repo files
  are stale (new ingestion requests, renamed confirm requests, the Mailpit
  pre-request scripts, the Logout URL fix, and the Change Email body all live
  only in cloud). Note: the Postman API/MCP can't reorder items and its WAF
  blocks pre-request scripts that call `pm.sendRequest`, so these are manual.
- **Broaden unhappy paths.** Only ingestion and the confirm flow exercise
  failures today; the rest are happy-path only. Add 401 on protected
  Evidence/Account routes, 404 / 422 on bad payloads, etc.
- **Cosmetic:** collection description still reads "Auth.Api + Evidence.Api"
  (changing it via API needs a full-collection rewrite).


## OpenAPI for public

When the app will be published out of dev env, it must provided an openapi enpoint


## Docs

The API docs is handled by the openapi and a frontend for the openapi docs.
However, there is not docs that explain the low level architeture decisions. There is only in code docs, no web.  

The docs should include:

- ER diagrams
- architecture overview (handled by the archimate repo)

That's only a rough plan - we need to chat about it 

## Delete user account

Use must have the option to delete their account in `Ambiquality.Auth.Api`. Add `account/{id}/delete` - only the user that has the same id can delete the account. 

## Evidence HTTP methods

There are HTTP `DELETE` methods in the evidence, the semantic meanings is wrong for the endpoints. Nothing get's actually deleted, only the status changes as every change is versioned. The question is if it should be left as is or corrected with `PUT`/`PATCH`/`POST`. 
