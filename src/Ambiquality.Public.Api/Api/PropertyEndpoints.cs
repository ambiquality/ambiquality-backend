using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// The observable-property vocabulary: the substance-specific concepts that
/// observations reference via <c>sosa:observedProperty</c>. Serving these makes
/// every minted property IRI dereferenceable (5-star linked data) and exposes the
/// QUDT quantity kind + unit and the link to authoritative external pollutant codes.
/// GET+HEAD, JSON / JSON-LD; rarely changes, so long-cached.
/// </summary>
public static class PropertyEndpoints
{
    public static void MapPropertyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup($"/{Constants.ApiVersion}/properties").WithTags("Properties");

        group.MapMethods("/", ["GET", "HEAD"], ListProperties)
            .WithName("ListProperties")
            .WithSummary("List observable properties")
            .WithDescription(
                "The platform's IEQ observable-property vocabulary. Each entry is the "
                + "sosa:observedProperty target for its parameter, with its QUDT quantity kind "
                + "+ applicable unit and any authoritative external match. JSON or JSON-LD.")
            .Produces<PropertyCollection>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);

        group.MapMethods("/{code}", ["GET", "HEAD"], GetPropertyByCode)
            .WithName("GetPropertyByCode")
            .WithSummary("Get an observable property by code")
            .WithDescription("The dereferenceable IRI target for a single observed property (JSON or JSON-LD).")
            .Produces<PropertyResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);
    }

    private static IResult ListProperties(HttpContext http, IConfiguration configuration)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        var iri = IriBuilder.ForRequest(http.Request, configuration);
        var items = ObservablePropertyVocabulary.All
            .Select(e => PropertyResponse.From(e, iri))
            .ToList();

        SetVocabularyCache(http);

        return format == ResponseFormat.JsonLd
            ? Results.Json(PropertyJsonLd.ToGraph(items), contentType: Constants.MediaTypeJsonLd)
            : Results.Ok(new PropertyCollection(items, Constants.LicenseIri));
    }

    private static IResult GetPropertyByCode(string code, HttpContext http, IConfiguration configuration)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        if (ObservablePropertyVocabulary.TryGet(code) is not { } entry)
            return Problems.NotFound($"No observable property with code '{code}'.");

        var iri = IriBuilder.ForRequest(http.Request, configuration);
        var property = PropertyResponse.From(entry, iri);

        SetVocabularyCache(http);

        return format == ResponseFormat.JsonLd
            ? Results.Json(PropertyJsonLd.ToResource(property, includeContext: true), contentType: Constants.MediaTypeJsonLd)
            : Results.Ok(property);
    }

    private static void SetVocabularyCache(HttpContext http) =>
        http.Response.Headers.CacheControl = $"public, max-age={Constants.ContextCacheSeconds}";
}
