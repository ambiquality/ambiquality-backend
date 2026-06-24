# Ambiquality Wiki

**Ambiquality** is an open-data platform for **Indoor Environmental Quality (IEQ)**
monitoring. Sensors deployed in rooms report measurements of indoor environmental
parameters — CO₂, temperature, relative humidity, particulate matter, VOCs, sound
pressure level and illuminance — and the platform republishes them as **linked open
data** under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).

It was built as a bachelor thesis at the Prague University of Economics and Business
(VŠE), author **Vilém Charwot**.

## Who this wiki is for

- **Sensor operators** — people who have registered a building, room and sensor in the
  admin app and now need to make their device send data. Start with
  [**Sending measurements**](sending-measurements.md).
- **Data consumers / developers** — see [how the data is published](publishing.md)
  (observations API, DCAT-AP catalog, monthly archives).

## The data flow in one paragraph

You register a **building → room → sensor** in the operator app and receive a one-time
**sensor key**. Your device then POSTs batches of readings to the **Ingestion API**,
which validates each reading and durably enqueues it. A background worker writes the
accepted measurements to the time-series store, and the **Public API** republishes them
as JSON, JSON-LD and CSV, plus a [DCAT-AP 3.0](https://semiceu.github.io/DCAT-AP/)
catalogue and monthly downloadable archives.

> **Measurements are immutable.** Once published, a measurement is never silently changed
> or deleted — a bad reading can only be flagged as invalid. Send carefully.
