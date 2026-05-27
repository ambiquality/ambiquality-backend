# TODO

Running backlog of outstanding work, read at the start of every session as a
complement to the session handoffs in `thoughts/shared/handoffs/`. Handoffs
capture *what happened*; this file captures *what still needs to happen*.

**Maintenance rule:** when an item is finished, **delete it** — do not check it
off and leave it. The file should only ever describe pending work, so it stays
small and cheap to load into context.

---

## Architecture

### Ingestion: introduce a queue + worker write path
**Why:** Ingestion currently validates and writes each measurement to the
database synchronously on the request thread. Under sustained load (NFR: ≥ 100
measurements/s) this couples request throughput directly to database write
capacity, risking DB overload and dropped readings when bursts exceed write
throughput.

**Goal:** Decouple accept-from-sensor from persist-to-DB. The ingestion endpoint
should validate and enqueue, then one or more workers drain the queue and write
to the `ieq` database in batches.

**Open design questions to resolve before implementing:**
- Queue technology — in-process channel vs. external broker (Redis is already in
  the stack; alternatives: RabbitMQ, NATS).
- Durability contract: NFR requires measurements persisted before HTTP 2xx
  ("no ack before write"). A fire-and-forget queue breaks this. Reconcile the
  queue design with the durability constraint (e.g. durable queue that the ack
  can depend on, or accept a relaxed contract).
- Backpressure / overflow behavior when the queue fills (reject with 503 vs.
  block vs. shed).
- Worker batching strategy and TimescaleDB bulk-insert approach.
