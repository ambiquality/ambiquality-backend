using System.Text;
using System.Text.Json;
using Ambiquality.Export.Worker.Persistence;
using Ambiquality.Export.Worker.Serialization;

namespace Ambiquality.Export.Worker.Tests;

/// <summary>Unit coverage for the streaming CSV and JSON-LD serializers.</summary>
public sealed class SerializerTests
{
    private static readonly MeasurementRow Co2 = new(
        Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        SensorId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
        ParameterCode: "co2",
        Value: 812.5,
        Unit: "ppm",
        ObservedAt: new DateTime(2026, 5, 28, 8, 0, 0, DateTimeKind.Utc),
        ReceivedAt: new DateTime(2026, 5, 28, 8, 0, 1, DateTimeKind.Utc),
        IsInvalid: false);

    private static MeasurementRow Particulate(string code) => new(
        Id: Guid.NewGuid(),
        SensorId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
        ParameterCode: code,
        Value: 12.3,
        Unit: "ug/m3",
        ObservedAt: new DateTime(2026, 5, 28, 8, 0, 0, DateTimeKind.Utc),
        ReceivedAt: new DateTime(2026, 5, 28, 8, 0, 1, DateTimeKind.Utc),
        IsInvalid: false);

    private static async IAsyncEnumerable<MeasurementRow> Rows(params MeasurementRow[] rows)
    {
        foreach (var r in rows)
            yield return r;
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Csv_WritesHeaderAndOneRowPerMeasurement()
    {
        using var stream = new MemoryStream();

        var count = await CsvMeasurementSerializer.WriteAsync(Rows(Co2), stream, CancellationToken.None);

        Assert.Equal(1, count);
        var lines = Encoding.UTF8.GetString(stream.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(CsvMeasurementSerializer.Header, lines[0]);
        // Same 10-column schema as the live endpoint, including the resolved QUDT URIs for co2.
        Assert.StartsWith(
            "11111111-1111-1111-1111-111111111111,22222222-2222-2222-2222-222222222222,co2,812.5,ppm," +
            "http://qudt.org/vocab/quantitykind/AmountOfSubstanceFraction,http://qudt.org/vocab/unit/PPM,",
            lines[1]);
        Assert.EndsWith(",false", lines[1].TrimEnd('\r'));
    }

    [Fact]
    public async Task JsonLd_WritesGraphOfSosaObservationsWithQudtUris()
    {
        using var stream = new MemoryStream();
        var serializer = new JsonLdMeasurementSerializer("https://example.org");

        var count = await serializer.WriteAsync(Rows(Co2), stream, CancellationToken.None);

        Assert.Equal(1, count);
        using var doc = JsonDocument.Parse(stream.ToArray());
        var root = doc.RootElement;
        Assert.Equal("https://example.org/v1/context/measurements.jsonld",
            root.GetProperty("@context").GetString());

        var graph = root.GetProperty("@graph").EnumerateArray().ToList();
        Assert.Single(graph);
        var obs = graph[0];
        Assert.Equal("https://example.org/v1/observations/11111111-1111-1111-1111-111111111111",
            obs.GetProperty("@id").GetString());
        Assert.Equal("sosa:Observation", obs.GetProperty("@type").GetString());
        Assert.Equal(812.5, obs.GetProperty("sosa:hasSimpleResult").GetDouble());
        Assert.Equal("http://qudt.org/vocab/unit/PPM",
            obs.GetProperty("qudt:unit").GetProperty("@id").GetString());
        Assert.Equal("https://example.org/v1/sensors/22222222-2222-2222-2222-222222222222",
            obs.GetProperty("sosa:madeBySensor").GetProperty("@id").GetString());

        // observedProperty is the specific minted property IRI; the dimensional quantity
        // kind lives on qudt:hasQuantityKind (not on observedProperty, the old bug).
        Assert.Equal("https://example.org/v1/properties/co2",
            obs.GetProperty("sosa:observedProperty").GetProperty("@id").GetString());
        Assert.Equal("http://qudt.org/vocab/quantitykind/AmountOfSubstanceFraction",
            obs.GetProperty("qudt:hasQuantityKind").GetProperty("@id").GetString());
    }

    [Fact]
    public async Task JsonLd_DistinguishesParticulateSizesOnObservedProperty()
    {
        using var stream = new MemoryStream();
        var serializer = new JsonLdMeasurementSerializer("https://example.org");

        await serializer.WriteAsync(Rows(Particulate("pm2_5"), Particulate("pm10")), stream, CancellationToken.None);

        using var doc = JsonDocument.Parse(stream.ToArray());
        var graph = doc.RootElement.GetProperty("@graph").EnumerateArray().ToList();
        Assert.Equal(2, graph.Count);

        var pm25 = graph[0].GetProperty("sosa:observedProperty").GetProperty("@id").GetString();
        var pm10 = graph[1].GetProperty("sosa:observedProperty").GetProperty("@id").GetString();

        // The whole point of the fix: PM2.5 and PM10 get DIFFERENT observed-property IRIs…
        Assert.Equal("https://example.org/v1/properties/pm2_5", pm25);
        Assert.Equal("https://example.org/v1/properties/pm10", pm10);
        Assert.NotEqual(pm25, pm10);

        // …even though they share the same dimensional quantity kind (mass density).
        Assert.Equal(
            graph[0].GetProperty("qudt:hasQuantityKind").GetProperty("@id").GetString(),
            graph[1].GetProperty("qudt:hasQuantityKind").GetProperty("@id").GetString());
    }

    [Fact]
    public async Task JsonLd_EmitsEmptyGraphForNoRows()
    {
        using var stream = new MemoryStream();
        var serializer = new JsonLdMeasurementSerializer("https://example.org");

        var count = await serializer.WriteAsync(Rows(), stream, CancellationToken.None);

        Assert.Equal(0, count);
        using var doc = JsonDocument.Parse(stream.ToArray());
        Assert.Empty(doc.RootElement.GetProperty("@graph").EnumerateArray());
    }
}
