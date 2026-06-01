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

    /// <summary>The wire media type for a resolved <see cref="ResponseFormat"/>.</summary>
    public static string MediaType(ResponseFormat format) => format switch
    {
        ResponseFormat.JsonLd => Constants.MediaTypeJsonLd,
        ResponseFormat.Csv => Constants.MediaTypeCsv,
        _ => Constants.MediaTypeJson
    };

    private static bool Matches(string candidate, string target) =>
        string.Equals(candidate, target, StringComparison.OrdinalIgnoreCase);
}
