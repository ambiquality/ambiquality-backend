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
        Assert.Equal("id,sensor_id,parameter_code,value,unit,quantity_kind_uri,unit_uri,observed_at,received_at,is_invalid", lines[1]);
        Assert.Equal(3, lines.Length - 2); // 3 valid co2 rows
    }

    [Fact]
    public async Task ExportCsv_CarriesLicenseLinkHeader()
    {
        var response = await Client.GetAsync("/v1/observations.csv");
        Assert.Contains(response.Headers.GetValues("Link"), v => v.Contains("rel=\"license\""));
    }
}
