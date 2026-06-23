namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Serves the stable JSON-LD <c>@context</c> that observation responses reference,
/// mapping the SOSA/SSN/QUDT/Dublin-Core terms and the custom <c>ambiq:</c> namespace.
/// </summary>
public static class ContextEndpoints
{
    // Long-cached because the term mapping rarely changes.
    private static readonly IReadOnlyDictionary<string, object?> Document = new Dictionary<string, object?>
    {
        ["@context"] = new Dictionary<string, object?>
        {
            ["sosa"] = "http://www.w3.org/ns/sosa/",
            ["ssn"] = "http://www.w3.org/ns/ssn/",
            ["qudt"] = "http://qudt.org/schema/qudt/",
            ["unit"] = "http://qudt.org/vocab/unit/",
            ["quantitykind"] = "http://qudt.org/vocab/quantitykind/",
            ["skos"] = "http://www.w3.org/2004/02/skos/core#",
            ["dcterms"] = "http://purl.org/dc/terms/",
            ["xsd"] = "http://www.w3.org/2001/XMLSchema#",
            ["ambiq"] = Constants.AmbiqNamespace,
            ["Observation"] = "sosa:Observation",
            ["observedProperty"] = new Dictionary<string, object?> { ["@id"] = "sosa:observedProperty", ["@type"] = "@id" },
            ["hasQuantityKind"] = new Dictionary<string, object?> { ["@id"] = "qudt:hasQuantityKind", ["@type"] = "@id" },
            ["madeBySensor"] = new Dictionary<string, object?> { ["@id"] = "sosa:madeBySensor", ["@type"] = "@id" },
            ["hasFeatureOfInterest"] = new Dictionary<string, object?> { ["@id"] = "sosa:hasFeatureOfInterest", ["@type"] = "@id" },
            ["hasSimpleResult"] = "sosa:hasSimpleResult",
            ["resultTime"] = new Dictionary<string, object?> { ["@id"] = "sosa:resultTime", ["@type"] = "xsd:dateTime" },
            ["receivedTime"] = new Dictionary<string, object?> { ["@id"] = "ambiq:receivedTime", ["@type"] = "xsd:dateTime" },
            ["isInvalid"] = new Dictionary<string, object?> { ["@id"] = "ambiq:isInvalid", ["@type"] = "xsd:boolean" },
            ["unit"] = new Dictionary<string, object?> { ["@id"] = "qudt:unit", ["@type"] = "@id" },
            ["license"] = new Dictionary<string, object?> { ["@id"] = "dcterms:license", ["@type"] = "@id" }
        }
    };

    public static void MapContextEndpoints(this WebApplication app)
    {
        app.MapMethods($"/{Constants.ApiVersion}/context/measurements.jsonld", ["GET", "HEAD"], (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = $"public, max-age={Constants.ContextCacheSeconds}";
            return Results.Json(Document, contentType: Constants.ContentTypeJsonLd);
        })
        .WithTags("Context")
        .WithName("GetMeasurementsContext")
        .WithSummary("JSON-LD context for observations")
        .WithDescription("The @context document that observation JSON-LD responses link to.")
        .Produces(StatusCodes.Status200OK, contentType: Constants.MediaTypeJsonLd);
    }
}
