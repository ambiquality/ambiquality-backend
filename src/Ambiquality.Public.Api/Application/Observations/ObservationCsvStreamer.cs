using System.Globalization;
using System.Text;
using Ambiquality.Core.Domain.Measurements;
using Ambiquality.Core.Domain.Vocabulary;
using Ambiquality.Public.Api.Api;

namespace Ambiquality.Public.Api.Application.Observations;

/// <summary>
/// Streams a filtered observation set as CSV directly to the response body, one row
/// at a time, so memory stays bounded regardless of export size. A leading
/// <c># license:</c> comment and a <c>Link: …; rel="license"</c> header carry the
/// CC BY 4.0 attribution (CSV has no body field for it), and a
/// <c>Link: …; rel="describedby"</c> points at the CSVW tabular schema so the CSV is
/// self-describing.
/// </summary>
public sealed class ObservationCsvStreamer(IAsyncEnumerable<Measurement>? rows, IriBuilder iri) : IResult
{
    private const string HeaderRow =
        "id,sensor_id,parameter_code,value,unit,observed_property_uri,quantity_kind_uri,unit_uri,observed_at,received_at,is_invalid";

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition = "attachment; filename=observations.csv";
        response.Headers.CacheControl = $"public, max-age={Constants.CacheSeconds}";
        response.Headers.Append("Link", $"<{Constants.LicenseIri}>; rel=\"license\"");
        response.Headers.Append("Link", $"<{iri.CsvMetadata()}>; rel=\"describedby\"; type=\"application/csvm+json\"");

        await using var writer = new StreamWriter(response.Body, new UTF8Encoding(false), 1 << 14, leaveOpen: true);
        await writer.WriteLineAsync($"# license: {Constants.LicenseIri}");
        await writer.WriteLineAsync(HeaderRow);

        if (rows is null)
        {
            await writer.FlushAsync();
            return;
        }

        await foreach (var m in rows.WithCancellation(httpContext.RequestAborted))
        {
            var qudt = QudtVocabulary.TryResolve(m.ParameterCode);
            var line = string.Join(',',
                m.Id.ToString("D"),
                m.SensorId.ToString("D"),
                Escape(m.ParameterCode),
                m.Value.ToString(CultureInfo.InvariantCulture),
                Escape(m.Unit),
                Escape(iri.Property(m.ParameterCode)),
                Escape(qudt?.QuantityKindUri),
                Escape(qudt?.UnitUri),
                m.ObservedAt.ToString("O", CultureInfo.InvariantCulture),
                m.ReceivedAt.ToString("O", CultureInfo.InvariantCulture),
                m.IsInvalid ? "true" : "false");
            await writer.WriteLineAsync(line);
        }

        await writer.FlushAsync();
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
