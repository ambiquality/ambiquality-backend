using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// The catalog codelists (<em>číselníky</em>) as dereferenceable SKOS concept schemes:
/// building type, room function, ventilation type, pollution source, room exposure and
/// sensor status. Serving these makes the catalog's code attributes resolvable concepts
/// with bilingual labels rather than bare strings. GET+HEAD, JSON / JSON-LD; long-cached.
/// </summary>
public static class CodelistEndpoints
{
    public static void MapCodelistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup($"/{Constants.ApiVersion}/codelists").WithTags("Codelists");

        group.MapMethods("/", ["GET", "HEAD"], ListCodelists)
            .WithName("ListCodelists")
            .WithSummary("List codelists")
            .WithDescription("The catalog's controlled vocabularies as SKOS concept schemes (JSON or JSON-LD).");

        group.MapMethods("/{scheme}", ["GET", "HEAD"], GetScheme)
            .WithName("GetCodelistScheme")
            .WithSummary("Get a codelist (concept scheme)")
            .WithDescription("A single codelist with its bilingual concepts (JSON or JSON-LD).");

        group.MapMethods("/{scheme}/{code}", ["GET", "HEAD"], GetConcept)
            .WithName("GetCodelistConcept")
            .WithSummary("Get a codelist concept")
            .WithDescription("The dereferenceable IRI target for a single codelist concept (JSON or JSON-LD).");
    }

    private static IResult ListCodelists(HttpContext http, IConfiguration configuration)
    {
        if (!TryFormat(http, out var format))
            return Problems.UnsupportedMediaType();

        var iri = IriBuilder.ForRequest(http.Request, configuration);
        SetVocabularyCache(http);

        if (format == ResponseFormat.JsonLd)
        {
            var schemes = Codelists.All.Select(c => CodelistSchemeResponse.From(c, iri));
            return Results.Json(CodelistJsonLd.ToGraph(schemes), contentType: Constants.MediaTypeJsonLd);
        }

        var refs = Codelists.All
            .Select(c => new CodelistSchemeRef(c.Scheme, iri.CodelistScheme(c.Scheme)))
            .ToList();
        return Results.Ok(new CodelistIndexResponse(refs, Constants.LicenseIri));
    }

    private static IResult GetScheme(string scheme, HttpContext http, IConfiguration configuration)
    {
        if (!TryFormat(http, out var format))
            return Problems.UnsupportedMediaType();

        if (Codelists.ByScheme(scheme) is not { } codelist)
            return Problems.NotFound($"No codelist with scheme '{scheme}'.");

        var iri = IriBuilder.ForRequest(http.Request, configuration);
        var response = CodelistSchemeResponse.From(codelist, iri);
        SetVocabularyCache(http);

        return format == ResponseFormat.JsonLd
            ? Results.Json(CodelistJsonLd.ToScheme(response, includeContext: true), contentType: Constants.MediaTypeJsonLd)
            : Results.Ok(response);
    }

    private static IResult GetConcept(string scheme, string code, HttpContext http, IConfiguration configuration)
    {
        if (!TryFormat(http, out var format))
            return Problems.UnsupportedMediaType();

        if (Codelists.ByScheme(scheme) is not { } codelist || codelist.TryGet(code) is not { } concept)
            return Problems.NotFound($"No codelist concept '{code}' in scheme '{scheme}'.");

        var iri = IriBuilder.ForRequest(http.Request, configuration);
        var response = CodelistConceptResponse.From(codelist, concept, iri);
        SetVocabularyCache(http);

        return format == ResponseFormat.JsonLd
            ? Results.Json(CodelistJsonLd.ToConcept(response, includeContext: true), contentType: Constants.MediaTypeJsonLd)
            : Results.Ok(response);
    }

    private static bool TryFormat(HttpContext http, out ResponseFormat format) =>
        ContentNegotiation.TryResolveFormat(http.Request, out format) && format != ResponseFormat.Csv;

    private static void SetVocabularyCache(HttpContext http) =>
        http.Response.Headers.CacheControl = $"public, max-age={Constants.ContextCacheSeconds}";
}
