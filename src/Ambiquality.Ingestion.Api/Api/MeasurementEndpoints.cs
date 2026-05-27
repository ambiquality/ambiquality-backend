using Ambiquality.Ingestion.Api.Application;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Ingestion.Api.Api;

public static class MeasurementEndpoints
{
    public const string SensorKeyHeader = "X-Sensor-Key";

    public static void MapMeasurementEndpoints(this WebApplication app)
    {
        app.MapPost("/measurements", IngestMeasurement)
            .WithName("IngestMeasurement")
            .WithTags("Measurements")
            .WithDescription("Validate and store a single sensor observation (F10/UC10).");
    }

    private static async Task<Results<Created<MeasurementAcceptedResponse>, ProblemHttpResult>> IngestMeasurement(
        IngestMeasurementRequest request,
        HttpContext context,
        IngestMeasurementHandler handler,
        CancellationToken cancellationToken)
    {
        var apiKey = context.Request.Headers[MeasurementEndpoints.SensorKeyHeader].ToString();

        var result = await handler.Handle(
            new IngestMeasurementCommand(
                SensorId: request.SensorId,
                PresentedApiKey: apiKey,
                ParameterCode: request.ParameterCode,
                Value: request.Value,
                ObservedAt: request.ObservedAt),
            cancellationToken);

        if (result.IsAccepted)
            return TypedResults.Created(
                (string?)null,
                new MeasurementAcceptedResponse(result.MeasurementId!.Value, result.ReceivedAt!.Value));

        return Problems.ToProblem(result.Rejection!.Value, result.Detail!);
    }
}
