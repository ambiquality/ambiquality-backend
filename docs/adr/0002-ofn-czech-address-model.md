# 2. Czech OFN address model for buildings

- **Status:** Accepted
- **Date:** 2026-06
- **Context:** Ambiquality backend (bachelor thesis, VŠE Prague)

## Context

The platform's scope was narrowed to the **Czech Republic only**. Two coupled questions about the
`Building` model followed:

1. **How to model a building's address.** The original model was a generic four-field postal
   record (`street`, `city`, `postcode`, `country`). For Czech open data, the relevant norm is the
   **OFN _Adresy_** (Otevřené formální normy, 2020-07-01,
   <https://ofn.gov.cz/adresy/2020-07-01/>), published by the Digital and Information Agency. It
   models an address as a node anchored on a **RÚIAN address-point code** (`kód adresního místa`),
   with structured components (`název_ulice`, `číslo_domovní` + `typ_čísla_domovního`,
   `číslo_orientační`, `název_obce`, `psč`, …) and an optional free-text fallback.

2. **Whether to keep location anonymization.** The catalog previously coarsened non-owners'
   building coordinates (`precise` / `street` ≈ 110 m / `municipality` ≈ 1.1 km) and suppressed
   address fields per an `anonymization_level`. Visualising this correctly was out of proportion
   for a bachelor thesis.

## Decision

### Adopt the OFN _Adresy_ standard, RÚIAN-anchored

The `Building` address is remodelled to the OFN structured form. The RÚIAN address-point code is
the **canonical anchor**; the structured Czech components are stored alongside it
(`address_point_code`, `street_name?`, `house_number`, `house_number_type` ∈ {č.p., č.ev.},
`orientation_number?`, `orientation_number_letter?`, `municipality_name`,
`municipality_part_name?`, `psc`, `district_name?`, `region_name?`). The country is dropped — the
platform is Czech-only.

The public JSON-LD emits a nested, scoped-context `ambiq:address` node shaped as an OFN `Adresa`
(`typ: "Adresa"`) carrying the dereferenceable `adresní_místo` IRI
(`linked.cuzk.cz/resource/ruian/adresni-misto/{code}`), the structured fields, and a composed
free-text `text` — so the OFN preference order (IRI → structured → text) is all provided.

### Reference the territorial elements by RÚIAN IRI, not just by name

In the OFN `adresa.jsonld` context every territorial element — `ulice`, `obec`, `část_obce`,
`okres`, `vúsc` (kraj) — is an **IRI** (`"@type": "@id"`), and each `název_*` label is a
**language-tagged string** (`{"cs": …}`). The first iteration stored only the names and emitted
only the `název_*` text as bare strings, so the dereferenceable territorial IRIs the standard's
examples show were never produced. To close that gap, the address now also stores the optional
**RÚIAN codes** for those elements (`street_code`, `municipality_code`, `municipality_part_code`,
`district_code`, `region_code`; migration `0011_OfnRuianCodes`). When a code is present the JSON-LD
node emits the matching IRI (e.g. `obec → linked.cuzk.cz/resource/ruian/obec/{code}`,
`vúsc → …/ruian/vusc/{code}`) alongside the `název_*` label, which is now correctly carried as
`{"cs": …}`. All codes are optional — a bare `adresní_místo` IRI already identifies the address.

**No live RÚIAN resolution.** Addresses are registrar-supplied and validated structurally only
(code positive, PSČ five digits, house-number type in the allowed set). Live lookup/validation
against the CUZK linked-data endpoint is named as future work, keeping the service self-contained
and its tests hermetic.

### Drop anonymization entirely

`AnonymizationLevel`, both `CoordinateMasking` implementations, the address-suppression switch and
the `anonymization` column are removed. Building coordinates are stored and returned **precisely to
every reader** — this is open data.

Rationale:

- **Standards alignment is the thesis contribution.** Emitting a conformant Czech `Adresa` node
  with a dereferenceable RÚIAN IRI is materially more valuable for an open-data catalogue than a
  bespoke address shape.
- **Anonymization was disproportionate.** Correctly visualising coarsened coordinates exceeds the
  thesis scope; the privacy trade-off of publishing precise coordinates is documented in the
  thesis text as a known risk rather than implemented as a half-measure.
- **Temporal versioning is unaffected.** Address and location remain attribute-history streams
  with half-open `tstzrange` validity and GiST exclusion constraints; only the payload columns
  changed (see migration `0010_OfnCzechAddress`).

## Consequences

- A clean-slate migration drops the four old address columns + `anonymization` and adds the OFN
  columns; only dev seed data existed, so no backfill was required. A follow-up migration
  (`0011_OfnRuianCodes`) adds the five nullable territorial RÚIAN code columns; existing rows keep
  `NULL` codes (and so emit no territorial IRI) until a registrar supplies them.
- Full **DCAT-AP-CZ** conformance is still out of reach for an individual (non-OVM) publisher — an
  unchanged, separately-documented limitation. RÚIAN *spatial-coverage* IRIs for the dataset as a
  whole remain out of scope; the address-point IRIs added here are per-building.
- Privacy of precise coordinates is now a documented thesis caveat, not an implemented control.
