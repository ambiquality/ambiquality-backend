using System.Globalization;
using System.Text;
using Ambiquality.Core.Domain.Vocabulary;
using Ambiquality.Export.Worker.Persistence;

namespace Ambiquality.Export.Worker.Serialization;

/// <summary>
/// Streams measurement rows as CSV into a writer, one row at a time, mirroring the
/// columns of the live CSV endpoint (<c>ObservationCsvStreamer</c>) so the monthly
/// archive and the live export share a single schema — including the QUDT
/// <c>quantity_kind_uri</c>/<c>unit_uri</c> semantic links resolved per parameter.
/// Returns the number of data rows written.
/// </summary>
public static class CsvMeasurementSerializer
{
    public const string Header =
        "id,sensor_id,parameter_code,value,unit,quantity_kind_uri,unit_uri,observed_at,received_at,is_invalid";

    public static async Task<long> WriteAsync(
        IAsyncEnumerable<MeasurementRow> rows, Stream destination, CancellationToken ct)
    {
        await using var writer = new StreamWriter(destination, new UTF8Encoding(false), 1 << 14, leaveOpen: true);
        await writer.WriteLineAsync(Header);

        long count = 0;
        await foreach (var m in rows.WithCancellation(ct))
        {
            var qudt = QudtVocabulary.TryResolve(m.ParameterCode);
            var line = string.Join(',',
                m.Id.ToString("D"),
                m.SensorId.ToString("D"),
                Escape(m.ParameterCode),
                m.Value.ToString(CultureInfo.InvariantCulture),
                Escape(m.Unit),
                Escape(qudt?.QuantityKindUri),
                Escape(qudt?.UnitUri),
                m.ObservedAt.ToString("O", CultureInfo.InvariantCulture),
                m.ReceivedAt.ToString("O", CultureInfo.InvariantCulture),
                m.IsInvalid ? "true" : "false");
            await writer.WriteLineAsync(line.AsMemory(), ct);
            count++;
        }

        await writer.FlushAsync(ct);
        return count;
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.AsSpan().IndexOfAny(",\"\n\r") >= 0)
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
