using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Ambiquality.Ingestion.Api.Infrastructure.Security;

namespace Ambiquality.Ingestion.Api.Application;

/// <summary>
/// Validates a batch of readings per UC10 (authenticate the sensor and confirm it is
/// active once for the batch, then for every reading check the sensor declares the
/// parameter and the value is in range) then hands the whole batch to the durable
/// ingestion queue. The batch is <strong>all-or-nothing</strong>: validation runs
/// before anything is enqueued, so a single bad reading rejects the request and nothing
/// is published. Both timestamps — the observation time (<c>ObservedAt</c>) and the
/// acceptance time (<c>ReceivedAt</c>) — are stamped here from the trusted server clock,
/// never taken from the sensor, in a single read shared by every reading in the batch, so
/// subsequent queue lag never shifts them. Durability NFR is reinterpreted as "durably
/// enqueued before ack": a publish failure yields <see cref="IngestRejectionReason.QueueUnavailable"/>
/// (→ 503) and acks nothing; the actual hypertable write is performed asynchronously by
/// Ingestion.Worker.
/// </summary>
public sealed class IngestMeasurementHandler(
    IClock clock,
    ISensorCatalog catalog,
    IeqDbContext ieq,
    IMeasurementQueuePublisher queue)
{
    private const string ActiveStatusCode = "active";

    public async Task<IngestMeasurementsResult> Handle(IngestMeasurementsCommand command, CancellationToken ct)
    {
        if (command.Readings is null || command.Readings.Count == 0)
            return IngestMeasurementsResult.Reject(
                IngestRejectionReason.EmptyBatch, "The batch must contain at least one reading.");

        var sensor = await catalog.FindSensorAsync(command.SensorId, ct);
        if (sensor is null || !SensorKeyHasher.Verify(command.PresentedApiKey, sensor.ApiKeyHash))
            return IngestMeasurementsResult.Reject(
                IngestRejectionReason.Unauthorized, "Unknown sensor or invalid API key.");

        if (sensor.StatusCode != ActiveStatusCode)
            return IngestMeasurementsResult.Reject(
                IngestRejectionReason.SensorNotActive,
                $"Sensor is '{sensor.StatusCode}', not active.");

        // Validate every reading before publishing anything — the batch is all-or-nothing.
        var seen = new HashSet<string>(command.Readings.Count);
        for (var i = 0; i < command.Readings.Count; i++)
        {
            var reading = command.Readings[i];

            if (!seen.Add(reading.ParameterCode))
                return IngestMeasurementsResult.Reject(
                    IngestRejectionReason.DuplicateParameter,
                    $"Reading {i} repeats parameter '{reading.ParameterCode}'; each parameter may appear once per batch.");

            if (!sensor.DeclaredParameterCodes.Contains(reading.ParameterCode))
                return IngestMeasurementsResult.Reject(
                    IngestRejectionReason.ParameterNotDeclared,
                    $"Reading {i}: sensor does not declare parameter '{reading.ParameterCode}'.");

            var range = await ieq.ParameterRanges.FindAsync([reading.ParameterCode], ct);
            if (range is null)
                return IngestMeasurementsResult.Reject(
                    IngestRejectionReason.ParameterNotDeclared,
                    $"Reading {i}: no permitted range is configured for parameter '{reading.ParameterCode}'.");

            if (!range.Contains(reading.Value))
                return IngestMeasurementsResult.Reject(
                    IngestRejectionReason.ValueOutOfRange,
                    $"Reading {i} ({reading.ParameterCode}): value {reading.Value} is outside the permitted range [{range.MinValue}, {range.MaxValue}].");
        }

        // Stamp the observation and acceptance times from the trusted server clock in a
        // single read shared by the whole batch — the sensor's own clock is never trusted,
        // so both reflect when the platform accepted the measurements.
        var now = clock.UtcNow;
        var messages = command.Readings
            .Select(reading => new MeasurementMessage(
                Id: Guid.NewGuid(),
                SensorId: command.SensorId,
                ParameterCode: reading.ParameterCode,
                Value: reading.Value,
                Unit: null,
                ObservedAt: now,
                ReceivedAt: now))
            .ToList();

        try
        {
            await queue.PublishAsync(messages, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return IngestMeasurementsResult.Reject(
                IngestRejectionReason.QueueUnavailable,
                "The ingestion queue is unavailable; the batch was not accepted.");
        }

        var accepted = messages
            .Select(m => new AcceptedReading(m.Id, m.ParameterCode))
            .ToList();
        return IngestMeasurementsResult.Accept(accepted, now);
    }
}
