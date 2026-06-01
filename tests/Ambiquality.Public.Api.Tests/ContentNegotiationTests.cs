using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

public sealed class ContentNegotiationTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    [Fact]
    public async Task DefaultIsJson()
    {
        var response = await Client.GetAsync("/v1/observations");
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task JsonLd_ReturnsGraphWithContext()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/observations");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/ld+json"));

        var response = await Client.SendAsync(request);
        Assert.Equal("application/ld+json", response.Content.Headers.ContentType?.MediaType);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(doc.TryGetProperty("@context", out _));
        Assert.True(doc.TryGetProperty("@graph", out var graph));
        var first = graph.EnumerateArray().First();
        Assert.Equal("sosa:Observation", first.GetProperty("@type").GetString());
    }

    [Fact]
    public async Task Csv_ViaAcceptHeader_StreamsCsv()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/observations");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));

        var response = await Client.SendAsync(request);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnsupportedMediaType_Returns406Problem()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/observations");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Head_ReturnsHeadersWithEmptyBody()
    {
        var request = new HttpRequestMessage(HttpMethod.Head, "/v1/observations");
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Empty(body);
    }
}
