using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Ambiquality.Ingestion.Api.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Ambiquality.Ingestion.Api.Application;

/// <summary>
/// Validates a batch of readings per UC10 (authenticate the sensor and confirm it is
/// active once for the batch, then for every reading check the sensor declares the
/// parameter, the unit matches the parameter's canonical unit and the value is in
/// range) then hands the whole batch to the durable
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
    IMeasurementQueuePublisher queue,
    IRateLimiter rateLimiter,
    IOptions<RateLimitOptions> rateLimitOptions)
{
    private const string ActiveStatusCode = "active";
    private readonly RateLimitOptions _rateLimit = rateLimitOptions.Value;

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

        // Per-sensor publish rate limit (keyed by sensor id — one API key per sensor).
        // Enforced before the per-reading DB validation so a sensor hammering the endpoint
        // cannot also hammer the parameter-range lookups. The window is the sensor's own
        // declared reporting interval, clamped to the >= 5 min floor.
        if (_rateLimit.Enabled)
        {
            var window = _rateLimit.WindowFor(sensor.ReportingIntervalSeconds);
            var decision = await rateLimiter.HitAsync(
                _rateLimit.KeyPrefix + command.SensorId, window, _rateLimit.PermitsPerWindow, ct);
            if (!decision.Allowed)
                return IngestMeasurementsResult.RateLimited(
                    $"Sensor is publishing faster than its {window}s reporting interval allows; "
                    + $"retry in {decision.RetryAfterSeconds}s or lengthen the interval in the sensor profile.",
                    decision.RetryAfterSeconds);
        }

        // Validate every reading before publishing anything — the batch is all-or-nothing.
        var seen = new HashSet<string>(command.Readings.Count);
        var canonicalUnits = new Dictionary<string, string?>(command.Readings.Count);
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

            // F10's "quantity AND unit" half: each parameter has exactly one canonical unit
            // (ieq.parameter_ranges.unit, mirroring the QUDT vocabulary), so declaring the
            // parameter in Evidence fixes the unit the sensor must report in.
            if (range.Unit is not null)
            {
                if (string.IsNullOrWhiteSpace(reading.Unit))
                    return IngestMeasurementsResult.Reject(
                        IngestRejectionReason.UnitMismatch,
                        $"Reading {i} ({reading.ParameterCode}): no unit declared; expected '{range.Unit}'.");

                if (!UnitsMatch(reading.Unit, range.Unit))
                    return IngestMeasurementsResult.Reject(
                        IngestRejectionReason.UnitMismatch,
                        $"Reading {i} ({reading.ParameterCode}): unit '{reading.Unit}' does not match the declared unit '{range.Unit}'.");
            }
            canonicalUnits[reading.ParameterCode] = range.Unit ?? Normalize(reading.Unit);

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
                // The canonical unit, not the sensor's raw string — validation proved they
                // agree, and storing the canonical form keeps the hypertable uniform.
                Unit: canonicalUnits[reading.ParameterCode],
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

    /// <summary>
    /// Units compare ordinally after trimming, except that the Greek small mu (U+03BC) is
    /// folded into the micro sign (U+00B5) — keyboards and SDKs produce both for "µg/m³".
    /// Case stays significant: unit symbols are case-sensitive (e.g. K vs k).
    /// </summary>
    private static bool UnitsMatch(string presented, string canonical) =>
        string.Equals(Normalize(presented), Normalize(canonical), StringComparison.Ordinal);

    private static string? Normalize(string? unit) =>
        unit?.Trim().Replace('μ', 'µ');
}
