# Conformance Review Addendum — gaps closed 2026-06-10

**Supplements:** [2026-06-09-thesis-requirements-conformance.md](2026-06-09-thesis-requirements-conformance.md)
**Scope:** Verdict changes only. Everything not listed below stands as reviewed on 2026-06-09.
**Delivered via:** backend PRs #46–#49 (all merged to `main` 2026-06-10), frontend PR #14
(merged 2026-06-10) + the sensor-installation form follow-up, and backend PR #45
(RÚIAN address lookup, merged 2026-06-10 before the review was distributed).

## Verdict changes

| ID | Was | Now | What changed |
|----|-----|-----|--------------|
| F08 | ⚠️ | ✅ | Optional installation attributes modelled as one composite temporal attribute: `sensor_installation_history` (position note, 3 distances, frequency, install + last-calibration dates; migration `0012_SensorInstallation`, deferred GiST no-overlap). Settable at registration (`installation` block) and afterwards (`PUT …/sensors/{id}/installation`), projected on every read incl. `asOf`. FE: the seven fields on SensorNewPage + a fourth `AttributeEditForm` on SensorEditPage + read-only rows on detail/history (`installation-form.tsx`). Backend PR #49. |
| F10 | ⚠️ | ✅ | Unit matching implemented: every reading must declare a `unit` equal to the parameter's canonical unit in `ieq.parameter_ranges` (mirrors `QudtVocabulary`); mismatch/missing → 422 `urn:ambiquality:ingestion:unit-mismatch`; accepted readings stored with the canonical unit string. "Veličině **a jednotce**" now holds. Backend PR #46. |
| F18 | ⚠️ | ✅ | `EntityDetailPage` is real (building/room/sensor with breadcrumb drill-down + charts); map dialog click-through lands on it. Public detail routes use the GUID the catalog resolves. Frontend PR #14. |
| SPO-04 | ❌ | ✅* | `postgres-backup` sidecar (10th GHCR image): daily `pg_dump` of auth/evidence/ieq + cluster globals, local retention, off-site copy to S3-compatible storage via `BACKUP_S3_*`. *The off-site copy is what satisfies SPO-04 — production MUST set the `BACKUP_S3_*` vars.* Backend PR #47. |
| POD-04 | ❌ | ✅ | `conf/vocabulary-extensions.json` mounted into Evidence/Ingestion/Public/Export (`Vocabulary__ExtensionsPath`); `VocabularyExtensionsLoader` applies codelist concepts and new quantities additively at startup (built-ins can never be redefined — backward compatible by construction). Extension quantities become declarable, validatable (auto-seeded `parameter_ranges` row) and published. Backend PR #48. |
| SYS-01 | ⚠️ | ✅* | `conf/Caddyfile.production`: domain-addressed site block → automatic TLS (Let's Encrypt) + 308 HTTP→HTTPS redirect. *Deploy must use this file (set `DOMAIN`/`ACME_EMAIL`, publish 80/443).* Backend PR #47. |
| EXT-03 | ❌ | ✅ | Already met before this round — the review predates backend PR #45: `RuianGeocoderClient` (configurable `Ruian:BaseUrl`, returns WGS84 coords) + FE `useAddressLookup` suggest/resolve. No verdict work needed beyond re-checking. |
| ROZ-03 | ⚠️ | ✅ | Shared `RangeSelector` (4 ranges) now on the operator sensor charts and the public sensor detail; no hardcoded `range:'day'` remains. Frontend PR #14. |
| DOK-01 | ⚠️ | ✅ | CataloguePage gained a per-dataset documentation section (field semantics, licence/terms) alongside the DCAT metadata. Frontend PR #14. |
| DOK-03 | ⚠️ | ✅ | `labelHint` on every registration field (building, room, sensor incl. the new installation fields) + a summary `/help` page in the navigation. Frontend PR #14 (+ installation hints in the follow-up). |
| F15 (UI gap) | note | ✅ | Footer links the Scalar API reference — the spec is surfaced in-app. Frontend PR #14. |

The "Frontend visitor UI gaps" section is closed in full: `catalog-browse` is a real
`/browse` page (F14 filters + offset pagination), `EntityDetailPage` is implemented,
and the spec link exists.

## Still open (knowingly deferred)

| ID | Verdict | Why deferred |
|----|---------|--------------|
| SPO-01 | ⚠️ | HA / multi-replica / LB is infrastructure provisioning, out of repo scope. |
| EXT-01 | ⚠️ | JSON-LD **is** RDF; a Turtle serialization would need dotNetRDF in Public.Api. Low value, requirement names Turtle only as an example. |
| PER-01 | ⚠️ | ppm↔mg/m³ display conversion intentionally omitted (priority 3, documented). |
| F17 note | ✅ | Unchanged: pre-generated archives are month-granular; subsetting is live. |
| VYK-01..03 | ✅ | Unchanged: design-level, not load-tested. |

## Updated summary

- **Fully met:** 38 of 41 requirements (was 30).
- **Partial:** SPO-01, EXT-01, PER-01 (was 11).
- **Not met:** none (was 3).

Production-deploy checklist for the starred verdicts: set `BACKUP_S3_*` (SPO-04) and
swap in `conf/Caddyfile.production` with `DOMAIN`/`ACME_EMAIL` + published 80/443 (SYS-01).
