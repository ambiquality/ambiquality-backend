using System.Net;
using System.Text.Json;
using Ambiquality.Ingestion.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace Ambiquality.Ingestion.Api.Tests.Api;

/// <summary>
/// The ingestion OpenAPI spec + Scalar reference must be exposed in ALL environments
/// (not just Development) so a sensor operator can read the contract after registering a
/// device. Verified against a Production-environment host.
/// </summary>
public sealed class ApiReferenceTests : IAsyncLifetime
{
    private IngestionApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new IngestionApiFactory();
        await _factory.InitializeAsync();
        // Force Production so this asserts the reference is no longer dev-gated.
        _client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
            .CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task OpenApiDocument_IsServed_InProduction()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        // The configured document Info — title and the read-only reference note.
        Assert.Contains("Ambiquality Ingestion API", body);
        Assert.Contains("/v1/measurements", body);
    }

    [Fact]
    public async Task OpenApiDocument_Declares_SensorKeySecurityScheme()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // The X-Sensor-Key API key is a declared security scheme, not just prose.
        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("SensorKey");
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("X-Sensor-Key", scheme.GetProperty("name").GetString());

        // The operation requires it and documents the throttled outcome + Retry-After header.
        var post = root.GetProperty("paths").GetProperty("/v1/measurements").GetProperty("post");
        Assert.Contains("SensorKey", post.GetProperty("security").EnumerateArray()
            .SelectMany(req => req.EnumerateObject().Select(p => p.Name)));
        var tooManyRequests = post.GetProperty("responses").GetProperty("429");
        Assert.True(tooManyRequests.GetProperty("headers").TryGetProperty("Retry-After", out _));
    }

    [Fact]
    public async Task ScalarReference_IsServed_InProduction()
    {
        var response = await _client.GetAsync("/scalar/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
