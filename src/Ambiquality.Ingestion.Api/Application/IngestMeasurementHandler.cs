using Ambiquality.Core.Domain.Measurements;
using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Ambiquality.Ingestion.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Ambiquality.Ingestion.Api.Application;

/// <summary>
/// Validates and persists a single observation per UC10: authenticate the sensor,
/// confirm it is active and declares the parameter, check the value range, then
/// store durably before the caller is acked (Durability NFR — no ack before write).
/// </summary>
public sealed class IngestMeasurementHandler(
    IClock clock,
    ISensorCatalog catalog,
    IeqDbContext ieq)
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

        var measurement = Measurement.Record(
            sensorId: command.SensorId,
            parameterCode: command.ParameterCode,
            value: command.Value,
            unit: null,
            observedAt: command.ObservedAt,
            receivedAt: clock.UtcNow);

        ieq.Measurements.Add(measurement);
        await ieq.SaveChangesAsync(ct);

        return IngestMeasurementResult.Accepted(measurement.Id, measurement.ReceivedAt);
    }
}
