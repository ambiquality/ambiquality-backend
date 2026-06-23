using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

public sealed class CsvEndpointsTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    [Fact]
    public async Task ExportCsv_HasLicenseCommentHeaderAndRows()
    {
        var response = await Client.GetAsync("/v1/observations.csv?parameterCode=co2");
        response.EnsureSuccessStatusCode();

        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.ToString());

        var lines = (await response.Content.ReadAsStringAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("# license:", lines[0]);
        Assert.Equal("id,sensor_id,parameter_code,value,unit,observed_property_uri,quantity_kind_uri,unit_uri,observed_at,received_at,is_invalid", lines[1]);
        Assert.Equal(3, lines.Length - 2); // 3 valid co2 rows

        // Each co2 row carries the substance-specific observed-property IRI.
        Assert.All(lines[2..], row =>
            Assert.Contains($"{PublicApiFactory.BaseIri}/v1/properties/co2", row));
    }

    [Fact]
    public async Task ExportCsv_CarriesLicenseLinkHeader()
    {
        var response = await Client.GetAsync("/v1/observations.csv");
        Assert.Contains(response.Headers.GetValues("Link"), v => v.Contains("rel=\"license\""));
    }

    [Fact]
    public async Task ExportCsv_CarriesDescribedByLinkToCsvwSchema()
    {
        var response = await Client.GetAsync("/v1/observations.csv");
        Assert.Contains(
            response.Headers.GetValues("Link"),
            v => v.Contains("rel=\"describedby\"") && v.Contains("/v1/schema/observations.csv-metadata.json"));
    }

    [Fact]
    public async Task CsvwSchema_IsServedWithExpectedColumns()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/schema/observations.csv-metadata.json");

        Assert.Equal("https://www.w3.org/ns/csvw", doc.GetProperty("@context").GetString());
        var columns = doc.GetProperty("tableSchema").GetProperty("columns").EnumerateArray()
            .Select(c => c.GetProperty("name").GetString()).ToList();
        Assert.Equal(
            new[] { "id", "sensor_id", "parameter_code", "value", "unit", "observed_property_uri", "quantity_kind_uri", "unit_uri", "observed_at", "received_at", "is_invalid" },
            columns);

        // observedProperty now points at the specific property column; the QUDT
        // quantity kind moved to its correct slot (qudt:hasQuantityKind).
        var byName = doc.GetProperty("tableSchema").GetProperty("columns").EnumerateArray()
            .ToDictionary(c => c.GetProperty("name").GetString()!, c => c);
        Assert.Equal("http://www.w3.org/ns/sosa/observedProperty",
            byName["observed_property_uri"].GetProperty("propertyUrl").GetString());
        Assert.Equal("http://qudt.org/schema/qudt/hasQuantityKind",
            byName["quantity_kind_uri"].GetProperty("propertyUrl").GetString());
    }
}
