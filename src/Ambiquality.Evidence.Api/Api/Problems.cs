using System.Globalization;
using Ambiquality.Evidence.Api.Application;
using Ambiquality.Evidence.Api.Domain;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Ambiquality.Evidence.Api.Domain.Sensors;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Evidence.Api.Api;

public sealed record ProblemDescriptor(int StatusCode, string Type, string Title, string Detail);

public static class Problems
{
    private const string TypePrefix = "urn:ambiquality:evidence:";

    public static ProblemDescriptor Describe(DomainException exception) => exception switch
    {
        BuildingNotFoundException => new ProblemDescriptor(
            StatusCodes.Status404NotFound,
            TypePrefix + "building-not-found",
            "Building not found",
            exception.Message),

        RoomNotFoundException => new ProblemDescriptor(
            StatusCodes.Status404NotFound,
            TypePrefix + "room-not-found",
            "Room not found",
            exception.Message),

        PollutionSourceNotFoundException => new ProblemDescriptor(
            StatusCodes.Status404NotFound,
            TypePrefix + "pollution-source-not-found",
            "Pollution source not found",
            exception.Message),

        SensorNotFoundException => new ProblemDescriptor(
            StatusCodes.Status404NotFound,
            TypePrefix + "sensor-not-found",
            "Sensor not found",
            exception.Message),

        MeasuredParameterNotFoundException => new ProblemDescriptor(
            StatusCodes.Status404NotFound,
            TypePrefix + "measured-parameter-not-found",
            "Measured parameter not found",
            exception.Message),

        ForbiddenException => new ProblemDescriptor(
            StatusCodes.Status403Forbidden,
            TypePrefix + "forbidden",
            "Forbidden",
            exception.Message),

        DuplicateUriSlugException => new ProblemDescriptor(
            StatusCodes.Status409Conflict,
            TypePrefix + "duplicate-uri-slug",
            "Duplicate URI slug",
            exception.Message),

        OverlappingValidityRangeException => new ProblemDescriptor(
            StatusCodes.Status409Conflict,
            TypePrefix + "overlapping-validity-range",
            "Overlapping validity range",
            exception.Message),

        UnknownCodelistCodeException => new ProblemDescriptor(
            StatusCodes.Status400BadRequest,
            TypePrefix + "unknown-codelist-code",
            "Unknown codelist code",
            exception.Message),

        // A missing open history row is a corruption symptom that should be
        // impossible if all state changes go through aggregate behavior; surface
        // it as a server fault rather than a client error.
        MissingOpenAttributeHistoryException => new ProblemDescriptor(
            StatusCodes.Status500InternalServerError,
            TypePrefix + "internal-server-error",
            "Internal server error",
            "An unexpected error occurred."),

        // Any other domain-rule violation is caused by the request (invalid
        // valid-from, empty value, non-UTC timestamp, out-of-range as-of, …).
        _ => new ProblemDescriptor(
            StatusCodes.Status400BadRequest,
            TypePrefix + "domain-rule-violation",
            "Domain rule violation",
            exception.Message)
    };

    public static ProblemHttpResult ToProblemResult(DomainException exception)
    {
        var descriptor = Describe(exception);
        return TypedResults.Problem(
            detail: descriptor.Detail,
            title: descriptor.Title,
            type: descriptor.Type,
            statusCode: descriptor.StatusCode);
    }

    /// <summary>
    /// Parses the optional <c>asOf</c> query parameter as a UTC instant,
    /// defaulting to "now". Returns <c>null</c> on success (with
    /// <paramref name="asOf"/> set), or a 400 problem result when the supplied
    /// value is not a valid timestamp.
    /// </summary>
    public static ProblemHttpResult? TryParseAsOf(HttpContext context, out DateTime asOf) =>
        TryParseUtcInstant(context, "asOf", out asOf);

    /// <summary>
    /// Parses the optional <c>validTo</c> query parameter as a UTC instant,
    /// defaulting to "now". Mirrors <see cref="TryParseAsOf"/>.
    /// </summary>
    public static ProblemHttpResult? TryParseValidTo(HttpContext context, out DateTime validTo) =>
        TryParseUtcInstant(context, "validTo", out validTo);

    private static ProblemHttpResult? TryParseUtcInstant(
        HttpContext context, string parameterName, out DateTime value)
    {
        value = DateTime.UtcNow;

        var raw = context.Request.Query[parameterName].FirstOrDefault();
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

        return TypedResults.Problem(
            detail: $"The '{parameterName}' query parameter must be a valid ISO-8601 timestamp.",
            title: "Invalid timestamp",
            type: TypePrefix + "invalid-timestamp",
            statusCode: StatusCodes.Status400BadRequest);
    }
}
