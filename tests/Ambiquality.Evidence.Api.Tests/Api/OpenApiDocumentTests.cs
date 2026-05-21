using System.Net;
using System.Text.Json;
using Ambiquality.Evidence.Api.Tests.Infrastructure;

namespace Ambiquality.Evidence.Api.Tests.Api;

/// <summary>
/// F15 — the API must publish an OpenAPI description. Guards against the
/// regression where the legacy <c>.WithOpenApi()</c> helper crashed document
/// generation for multi-method (GET+HEAD) routes.
/// </summary>
public sealed class OpenApiDocumentTests : IAsyncLifetime
{
    private EvidenceApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new EvidenceApiFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task OpenApiDocument_Generates_AndAdvertisesHeadOnReadRoutes()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var paths = document.RootElement.GetProperty("paths");
        var advertisesHead = paths
            .EnumerateObject()
            .Any(path => path.Value.TryGetProperty("head", out _));

        Assert.True(advertisesHead, "OpenAPI document should advertise at least one HEAD operation.");
    }
}
