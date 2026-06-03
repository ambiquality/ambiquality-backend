using Ambiquality.Ingestion.Api.Application;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Ingestion.Api.Api;

internal static class Problems
{
    // Mirrors the urn:ambiquality:<service>:<reason> problem `type` convention that
    // Auth/Evidence/Public already set on every problem response, so consumers can
    // branch on a stable, dereferenceable identifier rather than the status code.
    private const string TypePrefix = "urn:ambiquality:ingestion:";

    public static ProblemHttpResult ToProblem(IngestRejectionReason reason, string detail) => reason switch
    {
        IngestRejectionReason.Unauthorized => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized sensor", type: TypePrefix + "unauthorized"),
        IngestRejectionReason.SensorNotActive => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status403Forbidden,
            title: "Sensor not active", type: TypePrefix + "sensor-not-active"),
        IngestRejectionReason.ParameterNotDeclared => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Parameter not declared", type: TypePrefix + "parameter-not-declared"),
        IngestRejectionReason.ValueOutOfRange => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Value out of range", type: TypePrefix + "value-out-of-range"),
        IngestRejectionReason.QueueUnavailable => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Ingestion queue unavailable", type: TypePrefix + "queue-unavailable"),
        _ => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status400BadRequest,
            title: "Bad request", type: TypePrefix + "bad-request"),
    };
}
