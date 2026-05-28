using Ambiquality.Ingestion.Api.Application;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Ingestion.Api.Api;

internal static class Problems
{
    public static ProblemHttpResult ToProblem(IngestRejectionReason reason, string detail) => reason switch
    {
        IngestRejectionReason.Unauthorized => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized sensor"),
        IngestRejectionReason.SensorNotActive => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status403Forbidden, title: "Sensor not active"),
        IngestRejectionReason.ParameterNotDeclared => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Parameter not declared"),
        IngestRejectionReason.ValueOutOfRange => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Value out of range"),
        IngestRejectionReason.QueueUnavailable => TypedResults.Problem(
            detail, statusCode: StatusCodes.Status503ServiceUnavailable, title: "Ingestion queue unavailable"),
        _ => TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest),
    };
}
