using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Ambiquality.Public.Api.Api;

/// <summary>The representations this API can serve, selected by content negotiation.</summary>
public enum ResponseFormat
{
    Json,
    JsonLd,
    Csv
}

/// <summary>
/// Maps an HTTP <c>Accept</c> header onto a <see cref="ResponseFormat"/>, honouring
/// q-values and wildcards. JSON is the default when the header is absent or only
/// wildcards are offered; an <c>Accept</c> that lists exclusively unsupported,
/// non-wildcard types yields a 406 (the caller emits <c>Problems.UnsupportedMediaType</c>).
/// </summary>
public static class ContentNegotiation
{
    /// <summary>
    /// Resolves the best supported representation for the request. Returns
    /// <c>false</c> when the client explicitly demanded only unsupported types.
    /// </summary>
    public static bool TryResolveFormat(HttpRequest request, out ResponseFormat format)
    {
        format = ResponseFormat.Json;

        StringValues header = request.Headers.Accept;
        if (StringValues.IsNullOrEmpty(header))
            return true;

        if (!MediaTypeHeaderValue.TryParseList(header, out var accepted) || accepted.Count == 0)
            return true;

        // Highest quality first; LINQ OrderBy is stable, so equal-q entries keep
        // their header order (RFC 9110 §12.5.1 leaves equal-weight ties to the server).
        foreach (var media in accepted.OrderByDescending(m => m.Quality ?? 1.0))
        {
            var type = media.MediaType.Value;
            if (string.IsNullOrEmpty(type))
                continue;

            if (type is "*/*" or "application/*")
            {
                format = ResponseFormat.Json;
                return true;
            }

            if (Matches(type, Constants.MediaTypeJsonLd))
            {
                format = ResponseFormat.JsonLd;
                return true;
            }

            if (Matches(type, Constants.MediaTypeJson))
            {
                format = ResponseFormat.Json;
                return true;
            }

            if (Matches(type, Constants.MediaTypeCsv) || type == "text/*")
            {
                format = ResponseFormat.Csv;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Content negotiation for endpoints that serve <em>only</em> JSON-LD (e.g. the DCAT
    /// catalog). Absent/wildcard Accept defaults to <c>application/ld+json</c>. An explicit
    /// <c>application/json</c> is also accepted and returns the same JSON-LD document (the
    /// DCAT structure is valid JSON regardless of the <c>@</c>-keys). Only <c>text/csv</c>
    /// and other non-JSON types yield 406.
    /// </summary>
    public static bool TryResolveJsonLdOnly(HttpRequest request)
    {
        StringValues header = request.Headers.Accept;
        if (StringValues.IsNullOrEmpty(header))
            return true;

        if (!MediaTypeHeaderValue.TryParseList(header, out var accepted) || accepted.Count == 0)
            return true;

        // Walk types in decreasing quality order; skip unsupported types rather than
        // returning false immediately, so that Accept: text/csv, */*;q=0.9 still resolves
        // (the wildcard covers JSON-LD).
        // application/json is treated as an alias: clients that do not set Accept, or set
        // the generic application/json, must not be broken by a JSON-LD-only endpoint.
        foreach (var media in accepted.OrderByDescending(m => m.Quality ?? 1.0))
        {
            var type = media.MediaType.Value;
            if (string.IsNullOrEmpty(type))
                continue;

            if (type is "*/*" or "application/*"
                || Matches(type, Constants.MediaTypeJsonLd)
                || Matches(type, Constants.MediaTypeJson))
                return true;

            // This type is explicitly unsupported; try the next lower-quality entry.
        }

        return false;
    }

    /// <summary>The wire Content-Type value (with charset / header params) for a resolved format.</summary>
    public static string MediaType(ResponseFormat format) => format switch
    {
        ResponseFormat.JsonLd => Constants.ContentTypeJsonLd,
        ResponseFormat.Csv => Constants.ContentTypeCsv,
        _ => Constants.ContentTypeJson
    };

    private static bool Matches(string candidate, string target) =>
        string.Equals(candidate, target, StringComparison.OrdinalIgnoreCase);
}
