namespace Ambiquality.Public.Api.Api;

/// <summary>Shared helpers for the response headers every read endpoint sets.</summary>
internal static class ResponseHeaders
{
    /// <summary>
    /// Sets the public cache lifetime and a <c>describedby</c> link to the JSON Schema
    /// for <paramref name="resource"/> (e.g. "observation", "building").
    /// </summary>
    public static void SetListHeaders(HttpContext http, IriBuilder iri, string resource)
    {
        http.Response.Headers.CacheControl = $"public, max-age={Constants.CacheSeconds}";
        http.Response.Headers.Append("Link", $"<{iri.Schema(resource)}>; rel=\"describedby\"");
    }
}
