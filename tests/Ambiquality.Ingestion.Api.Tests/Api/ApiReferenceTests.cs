using System.Net;
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
    public async Task ScalarReference_IsServed_InProduction()
    {
        var response = await _client.GetAsync("/scalar/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
