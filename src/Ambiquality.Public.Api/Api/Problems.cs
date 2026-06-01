using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// RFC 9457 problem responses for the read-only public API. Unlike Evidence.Api
/// (which maps domain exceptions), Public.Api never mutates, so these are simple
/// factory helpers for the handful of client-input errors a reader can trigger.
/// All <c>type</c> URNs share the <c>urn:ambiquality:public:</c> prefix.
/// </summary>
public static class Problems
{
    private const string TypePrefix = "urn:ambiquality:public:";

    public static ProblemHttpResult NotFound(string detail) =>
        TypedResults.Problem(
            detail: detail,
            title: "Not found",
            type: TypePrefix + "not-found",
            statusCode: StatusCodes.Status404NotFound);

    public static ProblemHttpResult BadRequest(string title, string detail, string typeSuffix) =>
        TypedResults.Problem(
            detail: detail,
            title: title,
            type: TypePrefix + typeSuffix,
            statusCode: StatusCodes.Status400BadRequest);

    public static ProblemHttpResult InvalidCursor() =>
        BadRequest(
            "Invalid cursor",
            "The 'cursor' query parameter is not a valid pagination cursor.",
            "invalid-cursor");

    public static ProblemHttpResult InvalidTimestamp(string parameterName) =>
        BadRequest(
            "Invalid timestamp",
            $"The '{parameterName}' query parameter must be a valid ISO-8601 timestamp.",
            "invalid-timestamp");

    public static ProblemHttpResult InvalidBbox() =>
        BadRequest(
            "Invalid bounding box",
            "The 'bbox' query parameter must be four comma-separated numbers in the order "
                + "minLon,minLat,maxLon,maxLat with minLon ≤ maxLon and minLat ≤ maxLat.",
            "invalid-bbox");

    /// <summary>
    /// 406 Not Acceptable — the client's <c>Accept</c> header asked for a representation
    /// this API cannot produce. (Distinct from 415; negotiation failure, not request body.)
    /// </summary>
    public static ProblemHttpResult UnsupportedMediaType() =>
        TypedResults.Problem(
            detail: "The requested media type is not supported. "
                + "Available: application/json, application/ld+json, text/csv.",
            title: "Not acceptable",
            type: TypePrefix + "unsupported-media-type",
            statusCode: StatusCodes.Status406NotAcceptable);

    /// <summary>
    /// Parses an optional ISO-8601 timestamp filter (e.g. <c>from</c>/<c>to</c>) as a UTC
    /// instant. Returns <c>null</c> on success — <paramref name="value"/> is set to the parsed
    /// instant, or left <c>null</c> when the parameter is absent (no bound). Returns a 400
    /// problem when the supplied value is not a valid timestamp.
    /// </summary>
    public static ProblemHttpResult? TryParseUtcInstant(
        string? raw, string parameterName, out DateTime? value)
    {
        value = null;

        if (string.IsNullOrEmpty(raw))
            return null;

        if (DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            value = parsed;
            return null;
        }

        return InvalidTimestamp(parameterName);
    }
}
