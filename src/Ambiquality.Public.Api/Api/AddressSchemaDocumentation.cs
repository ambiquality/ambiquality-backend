using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// OpenAPI schema transformer that documents the building <see cref="AddressDto"/> in
/// the generated spec / Scalar UI. The DTO is a plain record, so without this the
/// address would appear as a list of bare typed fields with no explanation. Here we
/// attach a schema-level overview plus a per-field description so a newcomer can read
/// the spec and understand the Czech OFN <em>Adresy</em> / RÚIAN model — including how
/// each <c>*Code</c> field becomes a dereferenceable RÚIAN IRI in the JSON-LD form —
/// without consulting any external document.
/// </summary>
internal static class AddressSchemaDocumentation
{
    public static void Configure(OpenApiOptions options) =>
        options.AddSchemaTransformer((schema, context, _) =>
        {
            if (context.JsonTypeInfo.Type == typeof(AddressDto))
                DescribeAddress(schema);
            else if (context.JsonTypeInfo.Type == typeof(BuildingResponse))
                DescribeBuilding(schema);
            return Task.CompletedTask;
        });

    private static void DescribeBuilding(IOpenApiSchema schema)
    {
        if (schema is not OpenApiSchema s) return;
        s.Description =
            "A building in the open-data catalog, projected to its current state. `address` is "
            + "the Czech OFN address (see the Address schema). `latitude`/`longitude` are the "
            + "precise stored coordinates — this is open data, so they are returned in full to "
            + "every reader with no anonymization. `license` (CC BY 4.0) applies to the whole "
            + "response, and `iri` is the building's stable, dereferenceable identifier on this API.";
    }

    private static void DescribeAddress(IOpenApiSchema schema)
    {
        if (schema is not OpenApiSchema s) return;

        s.Description =
            "A building's postal address, modelled on the Czech open formal standard "
            + "**OFN _Adresy_** (2020-07-01, https://ofn.gov.cz/adresy/2020-07-01/) and anchored "
            + "on the national address register **RÚIAN**.\n\n"
            + "The single field `addressPointCode` (RÚIAN *kód adresního místa*) identifies the "
            + "address completely on its own; every other field is supplementary and may be null "
            + "(small municipalities have no street names, an address may have no orientation "
            + "number, and the RÚIAN territorial codes are filled in only when known).\n\n"
            + "When you request the building as Linked Data (`Accept: application/ld+json`) this "
            + "object is re-shaped into a conformant OFN `Adresa` node: each `*Code` below is "
            + "emitted as a **dereferenceable RÚIAN IRI** (e.g. "
            + "`https://linked.cuzk.cz/resource/ruian/obec/{municipalityCode}`) and each name is "
            + "carried as a Czech-language string (`{\"cs\": …}`). Those IRIs are stable global "
            + "identifiers you can use to join this data against RÚIAN and other linked datasets.";

        Describe(s, "addressPointCode",
            "RÚIAN address-point code (*kód adresního místa*) — the canonical, globally-unique "
            + "identifier of this exact address. On its own it fully identifies the address; the "
            + "remaining fields just spare a consumer a RÚIAN lookup. In JSON-LD it becomes the IRI "
            + "`https://linked.cuzk.cz/resource/ruian/adresni-misto/{code}`.");
        Describe(s, "streetName",
            "Street name (*název ulice*). Optional — many small municipalities have no named "
            + "streets, in which case the house number alone locates the building.");
        Describe(s, "houseNumber",
            "House number (*číslo domovní*) — the building's number within the municipality. Its "
            + "exact meaning depends on `houseNumberType`.");
        Describe(s, "houseNumberType",
            "Type of the house number (*typ čísla domovního*): `č.p.` = *číslo popisné* (the "
            + "permanent descriptive number of a regular building) or `č.ev.` = *číslo evidenční* "
            + "(a registration number used for recreational / temporary structures).");
        Describe(s, "orientationNumber",
            "Orientation number (*číslo orientační*) — the street-level number used for navigation "
            + "(the part after the slash in e.g. *1938/4*). Optional.");
        Describe(s, "orientationNumberLetter",
            "Letter suffix of the orientation number (*znak čísla orientačního*), e.g. the `a` in "
            + "*12a*. Optional.");
        Describe(s, "municipalityName", "Municipality name (*název obce*), e.g. *Praha*.");
        Describe(s, "municipalityPartName",
            "Name of the part of the municipality (*název části obce*), e.g. *Žižkov*. Optional.");
        Describe(s, "psc",
            "Postal code (*PSČ*) — five digits, stored without the conventional space (e.g. "
            + "`13067`, rendered as *130 67* in the free-text form).");
        Describe(s, "districtName",
            "District name (*název okresu*), e.g. *Brno-město*. Optional.");
        Describe(s, "regionName",
            "Region name (*název kraje*, i.e. the VÚSC), e.g. *Jihomoravský kraj*. Optional.");

        Describe(s, "streetCode",
            "RÚIAN street code. When present, the JSON-LD address emits it as the dereferenceable "
            + "IRI `https://linked.cuzk.cz/resource/ruian/ulice/{code}` (OFN `ulice`). Optional.");
        Describe(s, "municipalityCode",
            "RÚIAN municipality (obec) code → JSON-LD IRI "
            + "`https://linked.cuzk.cz/resource/ruian/obec/{code}` (OFN `obec`). Optional.");
        Describe(s, "municipalityPartCode",
            "RÚIAN municipality-part code → JSON-LD IRI "
            + "`https://linked.cuzk.cz/resource/ruian/cast-obce/{code}` (OFN `část_obce`). Optional.");
        Describe(s, "districtCode",
            "RÚIAN district (okres) code → JSON-LD IRI "
            + "`https://linked.cuzk.cz/resource/ruian/okres/{code}` (OFN `okres`). Optional.");
        Describe(s, "regionCode",
            "RÚIAN region / VÚSC (kraj) code → JSON-LD IRI "
            + "`https://linked.cuzk.cz/resource/ruian/vusc/{code}` (OFN `vúsc`). Optional.");
    }

    // Property names in the generated schema follow the JSON (camelCase) serialization.
    private static void Describe(OpenApiSchema schema, string property, string description)
    {
        if (schema.Properties is { } props
            && props.TryGetValue(property, out var p)
            && p is OpenApiSchema target)
        {
            target.Description = description;
        }
    }
}
