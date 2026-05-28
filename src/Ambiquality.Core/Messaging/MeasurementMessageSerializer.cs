using System.Text.Json;

namespace Ambiquality.Core.Messaging;

/// <summary>
/// Serializes <see cref="MeasurementMessage"/> to and from the string payload
/// carried in a Redis stream entry. Both the producer and the consumer use this
/// single implementation so the encoding can never diverge. DateTimes use the
/// default System.Text.Json ISO 8601 round-trip format, which preserves 100ns
/// ticks and the UTC kind ('Z') — required so <c>ReceivedAt</c> survives the
/// queue byte-for-byte.
/// </summary>
public static class MeasurementMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(MeasurementMessage message) =>
        JsonSerializer.Serialize(message, Options);

    public static MeasurementMessage Deserialize(string payload) =>
        JsonSerializer.Deserialize<MeasurementMessage>(payload, Options)
        ?? throw new FormatException("Measurement message payload deserialized to null.");
}
