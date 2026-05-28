using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Ambiquality.Ingestion.Api.Infrastructure.Security;

namespace Ambiquality.Ingestion.Api.Application;

/// <summary>
/// Validates a single observation per UC10 (authenticate the sensor, confirm it is
/// active and declares the parameter, check the value range) then hands it to the
/// durable ingestion queue. The acceptance timestamp (<c>ReceivedAt</c>) is stamped
/// here, on the request thread, so subsequent queue lag never shifts it. Durability
/// NFR is reinterpreted as "durably enqueued before ack": a publish failure yields
/// <see cref="IngestRejectionReason.QueueUnavailable"/> (→ 503) and acks nothing;
/// the actual hypertable write is performed asynchronously by Ingestion.Worker.
/// </summary>
public sealed class IngestMeasurementHandler(
    IClock clock,
    ISensorCatalog catalog,
    IeqDbContext ieq,
    IMeasurementQueuePublisher queue)
{
    private const string ActiveStatusCode = "active";

    public async Task<IngestMeasurementResult> Handle(IngestMeasurementCommand command, CancellationToken ct)
    {
        var sensor = await catalog.FindSensorAsync(command.SensorId, ct);
        if (sensor is null || !SensorKeyHasher.Verify(command.PresentedApiKey, sensor.ApiKeyHash))
            return IngestMeasurementResult.Reject(
                IngestRejectionReason.Unauthorized, "Unknown sensor or invalid API key.");

        if (sensor.StatusCode != ActiveStatusCode)
            return IngestMeasurementResult.Reject(
                IngestRejectionReason.SensorNotActive,
                $"Sensor is '{sensor.StatusCode}', not active.");

        if (!sensor.DeclaredParameterCodes.Contains(command.ParameterCode))
            return IngestMeasurementResult.Reject(
                IngestRejectionReason.ParameterNotDeclared,
                $"Sensor does not declare parameter '{command.ParameterCode}'.");

        var range = await ieq.ParameterRanges.FindAsync([command.ParameterCode], ct);
        if (range is null)
            return IngestMeasurementResult.Reject(
                IngestRejectionReason.ParameterNotDeclared,
                $"No permitted range is configured for parameter '{command.ParameterCode}'.");

        if (!range.Contains(command.Value))
            return IngestMeasurementResult.Reject(
                IngestRejectionReason.ValueOutOfRange,
                $"Value {command.Value} is outside the permitted range [{range.MinValue}, {range.MaxValue}] for '{command.ParameterCode}'.");

        var message = new MeasurementMessage(
            Id: Guid.NewGuid(),
            SensorId: command.SensorId,
            ParameterCode: command.ParameterCode,
            Value: command.Value,
            Unit: null,
            ObservedAt: command.ObservedAt,
            ReceivedAt: clock.UtcNow);

        try
        {
            await queue.PublishAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return IngestMeasurementResult.Reject(
                IngestRejectionReason.QueueUnavailable,
                "The ingestion queue is unavailable; the measurement was not accepted.");
        }

        return IngestMeasurementResult.Accepted(message.Id, message.ReceivedAt);
    }
}
