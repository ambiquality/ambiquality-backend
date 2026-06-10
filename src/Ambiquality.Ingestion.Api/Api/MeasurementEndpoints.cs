using Ambiquality.Ingestion.Api.Application;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Ingestion.Api.Api;

public static class MeasurementEndpoints
{
    public const string SensorKeyHeader = "X-Sensor-Key";

    public static void MapMeasurementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/measurements", IngestMeasurements)
            .WithName("IngestMeasurements")
            .WithTags("Measurements")
            .WithDescription(
                "Validate and enqueue a batch of sensor observations (F10/UC10). A sensor reports "
                + "one or more parameter readings in a single request — only the quantities it "
                + "actually measures. Every reading must declare its unit, which has to match the "
                + "canonical unit configured for the parameter. The batch is all-or-nothing: if any "
                + "reading fails validation the whole request is rejected and nothing is enqueued.");
    }

    private static async Task<Results<Accepted<MeasurementsAcceptedResponse>, ProblemHttpResult>> IngestMeasurements(
        IngestMeasurementsRequest request,
        HttpContext context,
        IngestMeasurementHandler handler,
        CancellationToken cancellationToken)
    {
        var apiKey = context.Request.Headers[SensorKeyHeader].ToString();

        var readings = (request.Readings ?? [])
            .Select(r => new MeasurementReadingInput(r.ParameterCode, r.Value, r.Unit))
            .ToList();

        var result = await handler.Handle(
            new IngestMeasurementsCommand(
                SensorId: request.SensorId,
                PresentedApiKey: apiKey,
                Readings: readings),
            cancellationToken);

        // 202, not 201: the batch is durably enqueued but not yet materialized into the
        // hypertable — the worker performs the write asynchronously.
        if (result.IsAccepted)
        {
            var measurements = result.Accepted!
                .Select(a => new AcceptedMeasurement(a.Id, a.ParameterCode))
                .ToList();
            return TypedResults.Accepted(
                (string?)null,
                new MeasurementsAcceptedResponse(result.ReceivedAt!.Value, measurements));
        }

        return Problems.ToProblem(result.Rejection!.Value, result.Detail!);
    }
}
