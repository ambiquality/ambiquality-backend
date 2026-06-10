# Thesis ↔ Code Conformance Review

**Date:** 2026-06-09
**Scope:** All requirements in `~/FIS/BP/pozadavky/` checked against backend
(`ambiquality-backend`) and frontend (`ambiquality-frontend`).
**Legend:** ✅ MET · ⚠️ PARTIAL · ❌ NOT MET

Verdicts combine backend + frontend evidence. Functional requirements F11–F15 are
*API* requirements satisfied by the backend; the frontend's matching visitor UI is
tracked separately under "Frontend UI gaps".

## Functional requirements (F01–F18)

| ID | Req | Verdict | Evidence / Notes |
|----|-----|---------|------------------|
| F01 | User registration | ✅ | BE `AuthEndpoints.cs:18` `POST /register` + email confirm; FE `RegisterPage.tsx:30` |
| F02 | User login | ✅ | BE `AuthEndpoints.cs:43` JWT pair, rate-limited; FE `LoginPage.tsx:23` |
| F03 | Logout | ✅ | BE `AccountEndpoints.cs:105` revokes refresh tokens; FE `RootLayout.tsx:120` |
| F04 | Change credentials | ✅ | BE `AccountEndpoints.cs:46,76` pwd + email-change-with-confirm; FE `AccountSettingsPage.tsx:71` |
| F05 | Building registration | ✅ | BE `Buildings/Commands.cs:4` (all attrs incl. OFN address, coords, years); FE `BuildingNewPage.tsx` |
| F06 | Room registration | ✅ | BE `Rooms/Commands.cs:3` (function, exposure, area+height, ventilation, pollution sources); FE `RoomNewPage.tsx` |
| F07 | Temporal validity of changes | ✅ | BE `Domain/Common/Validity.cs` half-open tstzrange + `asOf`; FE per-attr PUT `useTemporalEdit.ts`, `AsOfViewer.tsx` |
| F08 | Sensor registration | ⚠️ | BE `Sensors/Commands.cs:3` has manufacturer/model/serial/parameters(QUDT). **Missing optional attrs**: in-room position, distances to windows/doors/sources, declared frequency, install & last-calibration dates — not modelled in any sensor domain class. FE forms cover what BE exposes. |
| F09 | Sensor lifecycle | ✅ | BE `ChangeSensorStatusHandler` (active/maintenance/decommissioned) + `ChangeSensorPlacementHandler` (relocate); FE `SensorEditPage.tsx:34` |
| F10 | Measurement validation | ⚠️ | BE `IngestMeasurementHandler.cs:28` validates auth + active + parameter declared + value range. **Unit matching deferred** (Ingestion.Api README) — requirement explicitly demands "veličině **a jednotce**". Quantity+range only. |
| F11 | Public read API | ✅ | BE `ObservationEndpoints.cs`, `BuildingEndpoints.cs` etc. (JSON + JSON-LD, keyset history). *FE visitor browse UI not built — see UI gaps.* |
| F12 | Filtering | ✅ | BE `ObservationFilter.cs:11` (time/bbox/foi/sensor/parameter) + catalog filters |
| F13 | Pagination | ✅ | BE keyset `ObservationQuery.cs:46` + offset catalog with `next` links |
| F14 | Entity search | ✅ | BE building-type/room-function/exposure/parameter/bbox filters on catalog endpoints |
| F15 | Machine-readable API description | ✅ | BE `Public.Api/Program.cs:68` AddOpenApi + Scalar `/scalar/v1`. *FE doesn't surface a spec link — UI gap.* |
| F16 | DCAT-AP catalog metadata | ✅ | BE `CatalogEndpoints.cs:84` DCAT-AP 3.0 JSON-LD; FE `CataloguePage.tsx:32` HTML catalogue |
| F17 | Downloadable archive | ✅ | BE `MonthlyExportService.cs` pre-generated monthly CSV+JSON-LD zip in object storage; subset via live filtered `/v1/observations.csv`; FE `ArchivePage.tsx:36`. Note: pre-generated archives are month-granular; subsetting is live, not pre-generated. |
| F18 | Public interactive map | ⚠️ | FE `MapPage.tsx:26` MapLibre markers coloured by IEQ band + `ParameterFilter` + `MarkerTableFallback`; `BuildingDialog.tsx:50` on marker click shows latest values + `TimeSeriesChart`/`BoxPlot` + range selector. **Click-through target stub**: `EntityDetailPage.tsx` (`/buildings|rooms|sensors/:slug` public detail) is a 12-line placeholder, so "View building detail" leads to a stub. Map + in-dialog history work. |

## Non-functional, standards, constraints

| ID | Req | Verdict | Evidence / Notes |
|----|-----|---------|------------------|
| VYK-01 | Read p95<1s / p99<3s | ✅ | Hypertable + chunk pruning, composite index `InitialCreate.cs:73`, keyset paging, Redis cache `CacheSeconds=300`. Design-level; not load-tested. |
| VYK-02 | ≥100 measurements/s | ✅ | Queue+worker, bulk Npgsql insert `MeasurementBatchWriter.cs`, Redis AOF. Design-level. |
| VYK-03 | ≥50 concurrent reads | ✅ | Stateless read API + CORS + Redis cache. Design-level. |
| SPO-01 | ≥99% availability | ⚠️ | Stateless, health checks, `restart: unless-stopped`; **no HA/multi-replica/LB deploy config**. Operational concern, not provisioned. |
| SPO-02 | Durability before 2xx | ✅ | 202 only after XADD; Redis `appendfsync always`; 503 on enqueue fail `IngestMeasurementHandler.cs:69` |
| SPO-03 | Immutability of published data | ✅ | `Measurement.cs` soft-invalidate only; `ON CONFLICT DO NOTHING`; no UPDATE/DELETE |
| SPO-04 | Regular off-site backups (RPO≤24h) | ❌ | No `pg_dump`/WAL archiving/Barman/cron anywhere in repo. |
| POU-01 | cs+en UI, persisted | ✅ | FE i18next `config.ts:13` localStorage, `LanguageSwitch.tsx` always in header |
| POU-02 | WCAG 2.1 AA | ✅ | FE axe-core jsdom `test/a11y.ts` + Playwright `e2e/a11y.spec.ts` (incl. color-contrast), skip-link, ARIA |
| POU-03 | Responsive 360px→desktop | ✅ | FE `e2e/responsive.spec.ts:9` no-overflow at 360px; responsive Chakra props |
| POD-01 | Versioned API | ✅ | BE `/v1/` prefix on all public routes (`Constants.cs:13`). Single version exists. |
| POD-02 | Containerized deployment | ✅ | BE compose + 9 images + `compose.ghcr.yml`; FE static build |
| POD-03 | Automated migrations | ✅ | BE EF Core, `*-migrate` containers at startup, versioned in repo |
| POD-04 | Extensible codelists/vocabularies | ❌ | `Codelists.cs:49` hard-coded C# constants — adding a building type/room function/quantity needs code change + redeploy. No admin/DB/config mechanism. |
| DOK-01 | Dataset docs as HTML | ⚠️ | FE `CataloguePage.tsx` HTML dataset metadata + BE Scalar API ref. No single cohesive per-dataset HTML page with field semantics + terms-of-use. |
| DOK-02 | Machine-readable spec + HTML from same source | ✅ | BE OpenAPI `/openapi/v1.json` + Scalar `/scalar/v1` |
| DOK-03 | Contextual help on forms + summary help page | ⚠️ | FE `FormField.tsx:28` `labelHint` tooltips but only on ~2 RÚIAN fields; **no summary HTML help page in nav** (AboutPage is project info, not a user guide). |
| STA-01 | OFN / 5-star linked data | ✅ | BE dereferenceable IRIs (properties, codelists, RÚIAN address), SKOS, `@context` |
| STA-02 | DCAT-AP 3.0.0 | ✅ | BE pinned 3.0.0 context `CatalogEndpoints.cs:44` (DCAT-AP-CZ partial — needs OVM identity, documented) |
| STA-03 | SSN/SOSA | ✅ | BE `ObservationContracts.cs:81` sosa:Observation/observedProperty/madeBySensor etc. |
| STA-04 | QUDT | ✅ | BE `QudtVocabulary.cs:9` quantitykind + unit URIs on every observation |
| STA-05 | RFC 9457 ProblemDetails | ✅ | All services `Problems.cs` + `urn:ambiquality:*` types |
| STA-06 | Open source license | ✅ | MIT in both repos |
| SYS-01 | HTTPS only + redirect | ⚠️ | Committed `conf/Caddyfile` binds `:80` only (no domain → no auto-TLS/redirect); port 443 exposed in ghcr compose. FE base URLs env-driven HTTPS. Prod needs domain-addressed Caddyfile. |
| SYS-02 | CORS on public read endpoints | ✅ | BE `Public.Api/Program.cs:57` AllowAnyOrigin/Header/Method |
| SYS-03 | Persistent identifiers | ✅ | BE immutable server-generated slugs/GUIDs, never reassigned |
| SYS-04 | Pre-generated archive from storage | ✅ | BE `MonthlyExportService.cs` batch monthly → storage; not per-request |
| EXT-01 | REST/HTTP, content negotiation, JSON-LD default | ⚠️ | BE GET/HEAD + Accept negotiation, JSON-LD default + CSV. **No Turtle/other RDF serialization** (JSON-LD is RDF; Turtle was the requirement's example). |
| EXT-02 | Sensor ingestion + device auth | ✅ | BE `X-Sensor-Key` SHA-256 `IngestMeasurementHandler.cs:31`; key shown once |
| EXT-03 | External configurable geocoding | ❌ | No geocoding client/config anywhere. Address entry is manual RÚIAN-code-first (documented design choice). |
| EXT-04 | External configurable map tiles | ✅ | FE `env.ts:33` `VITE_MAP_STYLE_URL` + attribution, no provider hardcoded |
| ROZ-01 | Map as landing hub | ✅ | FE `router.tsx:42` MapPage is `/` index |
| ROZ-02 | Breadcrumb navigation | ✅ | FE `Breadcrumb.tsx:34` building→room→sensor trail, clickable |
| ROZ-03 | Sensor-detail time chart w/ range selector | ⚠️ | FE operator `SensorCharts.tsx:51` **hardcoded `range:'day'`, no selector**; public sensor detail is a stub. Map dialog has a 4-range selector but at building level. |
| KON-01 | Consistent terminology | ✅ | FE i18n `glossary` namespace, single canonical cs/en term, no-synonyms rule |
| KON-02 | Consistent form behavior | ✅ | FE `FormField.tsx` validate-on-blur, field errors, required marker, `FormActions` button position |
| PER-01 | Preferred display units | ⚠️ | FE temp °C/°F + pressure conversions work (`units/conversions.ts`); **ppm↔mg·m³ (the requirement's example) intentionally not implemented**. Priority 3. |
| VZH-01 | Single established design system | ✅ | FE Chakra UI v3 only, "no other UI kit" |

## Frontend visitor UI gaps (backend FR met, consumer UI incomplete)

These don't fail F11–F15 as *API* requirements but leave UC11–UC14/UC18 web-app
surface incomplete:

- **catalog-browse feature empty** — no visitor list/filter/search/paginate UI for
  observations or entities (`catalog-browse/` contains only a README). Browsing is
  only via the map.
- **`EntityDetailPage.tsx` is a placeholder stub** — the public per-entity detail
  page (map click-through target, breadcrumb target) is unimplemented.
- **No in-app OpenAPI spec link (F15 surfacing)** — spec only consumed by codegen.

## Summary

- **Fully met:** 30 of 41 requirements.
- **Partial:** F08, F10, F17(noted), F18, SPO-01, DOK-01, DOK-03, SYS-01, EXT-01,
  ROZ-03, PER-01.
- **Not met:** SPO-04 (backups), POD-04 (codelist extensibility), EXT-03 (geocoding).

Most gaps are either low-priority (PER-01 p3, EXT-01 Turtle), operational/deploy
concerns (SPO-01, SPO-04, SYS-01), or optional attributes (F08). The two substantive
functional gaps are **F10 unit matching** (priority 1) and the **frontend visitor
browse/detail UI** (UC18 completeness).
