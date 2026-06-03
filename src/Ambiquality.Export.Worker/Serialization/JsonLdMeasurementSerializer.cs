using System.Globalization;
using System.Text.Json;
using Ambiquality.Core.Domain.Vocabulary;
using Ambiquality.Export.Worker.Persistence;

namespace Ambiquality.Export.Worker.Serialization;

/// <summary>
/// Streams measurement rows as a JSON-LD document — a single <c>@context</c> reference
/// and a <c>@graph</c> array of SSN/SOSA observations — mirroring the per-observation
/// shape produced by Public.Api's <c>ObservationJsonLd.ToResource</c> (specific
/// <c>sosa:observedProperty</c> IRI, dimensional kind on <c>qudt:hasQuantityKind</c>).
/// Written incrementally with a <see cref="Utf8JsonWriter"/> so a whole month never
/// buffers in memory. Returns the number of observations written.
/// </summary>
public sealed class JsonLdMeasurementSerializer(string baseIri)
{
    private const string LicenseIri = "https://creativecommons.org/licenses/by/4.0/";

    private readonly string _root = $"{baseIri.TrimEnd('/')}/v1";

    public async Task<long> WriteAsync(
        IAsyncEnumerable<MeasurementRow> rows, Stream destination, CancellationToken ct)
    {
        await using var writer = new Utf8JsonWriter(destination);

        writer.WriteStartObject();
        writer.WriteString("@context", $"{_root}/context/measurements.jsonld");
        writer.WriteString("license", LicenseIri);
        writer.WritePropertyName("@graph");
        writer.WriteStartArray();

        long count = 0;
        await foreach (var m in rows.WithCancellation(ct))
        {
            WriteObservation(writer, m);
            count++;

            if (writer.BytesPending > 1 << 15)
                await writer.FlushAsync(ct);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(ct);
        return count;
    }

    private void WriteObservation(Utf8JsonWriter writer, MeasurementRow m)
    {
        // The specific, substance-distinguishing property goes on sosa:observedProperty;
        // the shared dimensional quantity kind (which collapses PM2.5/PM10, CO₂/eCO₂/VOC)
        // belongs on qudt:hasQuantityKind. Mirrors Public.Api's ObservationJsonLd.ToResource.
        var property = ObservablePropertyVocabulary.TryGet(m.ParameterCode);
        var qudt = property?.Qudt;

        writer.WriteStartObject();
        writer.WriteString("@id", $"{_root}/observations/{m.Id:D}");
        writer.WriteString("@type", "sosa:Observation");

        if (property is { } p)
        {
            writer.WritePropertyName("sosa:observedProperty");
            WriteIdObject(writer, $"{_root}/properties/{p.Code}");
        }

        if (qudt is { } q)
        {
            writer.WritePropertyName("qudt:hasQuantityKind");
            WriteIdObject(writer, q.QuantityKindUri);
        }

        writer.WritePropertyName("sosa:madeBySensor");
        WriteIdObject(writer, $"{_root}/sensors/{m.SensorId:D}");

        writer.WriteNumber("sosa:hasSimpleResult", m.Value);

        if (qudt is { } q2)
        {
            writer.WritePropertyName("qudt:unit");
            WriteIdObject(writer, q2.UnitUri);
        }

        writer.WriteString("sosa:resultTime", m.ObservedAt.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("ambiq:receivedTime", m.ReceivedAt.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteBoolean("ambiq:isInvalid", m.IsInvalid);
        writer.WriteString("license", LicenseIri);
        writer.WriteEndObject();
    }

    private static void WriteIdObject(Utf8JsonWriter writer, string id)
    {
        writer.WriteStartObject();
        writer.WriteString("@id", id);
        writer.WriteEndObject();
    }
}
