# Sending measurements

This guide walks a **sensor operator** through making a registered device submit
measurements to the Ambiquality Ingestion API.

The interactive, always-current contract for the single endpoint below is the
[**API reference**](api-reference.md) (Scalar). This page is the narrative version:
the *why*, the rules, and copy-pasteable examples.

## 1. Prerequisites

Before a device can send anything you must, in the **operator app**:

1. Register a **building**, then a **room** inside it, then a **sensor** in that room.
2. Declare, on the sensor, **which parameters it measures** (e.g. `co2`, `temperature`).
   A sensor may only report parameters it has declared.
3. On creation the app shows the sensor's **secret key** exactly once — it looks like
   `amq_sk_…`. **Copy it then; it is never shown again and cannot be recovered.** If you
   lose it, rotate the key on the sensor (a new key invalidates the old one).

The key authenticates the device. Treat it like a password: keep it on the device, never
commit it to source control, never put it in a URL.

## 2. The endpoint

```
POST {base}/ingestion/v1/measurements
X-Sensor-Key: amq_sk_your_key_here
Content-Type: application/json
```

- `{base}` is the platform API host (production: `https://api.ambiquality.org`).
- The secret key travels in the **`X-Sensor-Key` header**, never in the body or the URL.
- The body is a **batch** of readings from **one** sensor.

## 3. The request body

A sensor sends every quantity it measured at that moment **together, in one batch**:

```json
{
  "sensorId": "33333333-3333-3333-3333-333333333333",
  "readings": [
    { "parameterCode": "co2",         "value": 812,  "unit": "ppm" },
    { "parameterCode": "temperature", "value": 21.4, "unit": "°C"  }
  ]
}
```

Rules for each reading:

| Field | Rule |
|-------|------|
| `parameterCode` | Must be one the sensor **declared** it measures. Otherwise the batch is rejected (`parameter-not-declared`). |
| `value` | A number. Must fall inside the parameter's permitted range, else `value-out-of-range`. |
| `unit` | Must equal the **canonical unit** for that parameter, else `unit-mismatch`. See [Parameters & units](parameters-and-units.md). |

Two principles worth internalising:

- **The batch is all-or-nothing.** If *any* reading is invalid, the **whole request is
  rejected** and *nothing* is stored. Fix the reading and resend the batch.
- **You do not send a timestamp.** The server stamps the ingestion time from its own
  trusted clock at the moment it accepts the batch, so an unsynchronised device clock can
  never skew the recorded time. (All readings in one batch share that single instant.)

## 4. How often to send — the rate limit

Each sensor declares a **reporting interval** (its `measurement_frequency_seconds`,
floored at 5 minutes). The API allows **one batch per interval** per sensor. Send faster
and you get **`429 Too Many Requests`** with a **`Retry-After`** header telling you how
many seconds to wait. A well-behaved device reads `Retry-After` and backs off exactly
that long.

So: bundle everything measured in a cycle into **one batch per cycle**, don't fire a
request per parameter.

## 5. Responses

| Status | Meaning | What to do |
|--------|---------|-----------|
| **202 Accepted** | Batch durably enqueued (not yet written to the DB — a worker materialises it shortly). The body echoes `receivedAt` and the assigned measurement `id` per reading. | Done. Optionally log the ids. |
| **401 Unauthorized** | The `X-Sensor-Key` is missing or wrong. | Check the header / rotate the key. |
| **403 Forbidden** | The sensor exists but is **not active** (e.g. in maintenance or retired). | Re-activate it in the operator app. |
| **422 Unprocessable Entity** | A reading failed validation — empty batch, duplicate parameter, undeclared parameter, value out of range, or unit mismatch. The `type`/`title` say which. | Fix the offending reading and resend. |
| **429 Too Many Requests** | You exceeded the sensor's reporting interval. | Wait `Retry-After` seconds, then resend. |
| **503 Service Unavailable** | The durable queue could not accept the batch (nothing was stored). | Retry with backoff; the platform fsyncs every enqueue, so a 2xx is a real durability guarantee and a 503 means *not stored*. |

Errors use [RFC 9457 problem+json](https://www.rfc-editor.org/rfc/rfc9457) with a stable
`type` of the form `urn:ambiquality:ingestion:<reason>` — branch on that, not on the
status code alone.

## 6. Examples

### curl

```bash
curl -sS -X POST "https://api.ambiquality.org/ingestion/v1/measurements" \
  -H "X-Sensor-Key: $AMQ_SENSOR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
        "sensorId": "33333333-3333-3333-3333-333333333333",
        "readings": [
          { "parameterCode": "co2",         "value": 812,  "unit": "ppm" },
          { "parameterCode": "temperature", "value": 21.4, "unit": "°C"  }
        ]
      }'
```

### Python

```python
import os, requests

resp = requests.post(
    "https://api.ambiquality.org/ingestion/v1/measurements",
    headers={"X-Sensor-Key": os.environ["AMQ_SENSOR_KEY"]},
    json={
        "sensorId": "33333333-3333-3333-3333-333333333333",
        "readings": [
            {"parameterCode": "co2", "value": 812, "unit": "ppm"},
            {"parameterCode": "temperature", "value": 21.4, "unit": "°C"},
        ],
    },
    timeout=10,
)

if resp.status_code == 202:
    print("accepted:", resp.json()["receivedAt"])
elif resp.status_code == 429:
    retry = int(resp.headers.get("Retry-After", "300"))
    print(f"rate limited, retry in {retry}s")
else:
    print("rejected:", resp.status_code, resp.json())
```

## 7. Next

- [Parameters & units](parameters-and-units.md) — the canonical units you must send.
- [API reference](api-reference.md) — the live, machine-readable contract.
- [How the data is published](publishing.md) — where your measurements end up.
