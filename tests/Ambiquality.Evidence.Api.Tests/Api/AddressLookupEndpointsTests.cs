using System.Net;
using System.Net.Http.Json;
using Ambiquality.Evidence.Api.Api;
using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Tests.Infrastructure;
using Ambiquality.Evidence.Api.Tests.TestSupport;

namespace Ambiquality.Evidence.Api.Tests.Api;

/// <summary>
/// Endpoint coverage for the RÚIAN address-lookup convenience: authentication gating, the
/// suggest/resolve happy paths, a no-match 404 and an upstream-failure 502 — all against a fake
/// <see cref="IAddressGeocoder"/> so the tests never reach the external ČÚZK service.
/// </summary>
public sealed class AddressLookupEndpointsTests : IAsyncLifetime
{
    private readonly FakeGeocoder _geocoder = new();
    private EvidenceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private HttpClient _anonymous = null!;

    public async Task InitializeAsync()
    {
        _factory = new EvidenceApiFactory { Geocoder = _geocoder };
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
        _anonymous = _factory.CreateClient();
        _anonymous.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Suggest_WhenAnonymous_Returns401()
    {
        var response = await _anonymous.GetAsync("/v1/address-lookup/suggest?q=Revoluční");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Suggest_ReturnsSuggestions()
    {
        _geocoder.Suggestions = [new AddressSuggestion("Revoluční 93, 25261 Dobrovíz", "1_555742")];

        var body = await _client.GetFromJsonAsync<AddressSuggestionsResponse>(
            "/v1/address-lookup/suggest?q=Revoluční 93");

        Assert.NotNull(body);
        var only = Assert.Single(body!.Suggestions);
        Assert.Equal("1_555742", only.Key);
    }

    [Fact]
    public async Task Resolve_ReturnsResolvedAddress()
    {
        _geocoder.Resolved = SampleAddress();

        var response = await _client.GetAsync("/v1/address-lookup/resolve?key=1_555742");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var resolved = await response.Content.ReadFromJsonAsync<ResolvedAddress>();
        Assert.NotNull(resolved);
        Assert.Equal(6265154, resolved!.AddressPointCode);
        Assert.Equal("Dobrovíz", resolved.MunicipalityName);
    }

    [Fact]
    public async Task Resolve_NoMatch_Returns404Problem()
    {
        _geocoder.Resolved = null;

        var response = await _client.GetAsync("/v1/address-lookup/resolve?key=1_000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDocument>();
        Assert.Equal("urn:ambiquality:evidence:address-not-found", problem!.Type);
    }

    [Fact]
    public async Task Resolve_MissingKey_Returns400()
    {
        var response = await _client.GetAsync("/v1/address-lookup/resolve");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_WhenGeocoderFails_Returns502Problem()
    {
        _geocoder.Throw = new HttpRequestException("upstream down");

        var response = await _client.GetAsync("/v1/address-lookup/resolve?key=1_555742");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDocument>();
        Assert.Equal("urn:ambiquality:evidence:address-lookup-unavailable", problem!.Type);
    }

    private static ResolvedAddress SampleAddress() => new(
        AddressPointCode: 6265154,
        StreetName: "Revoluční",
        HouseNumber: 93,
        HouseNumberType: "č.p.",
        OrientationNumber: null,
        OrientationNumberLetter: null,
        MunicipalityName: "Dobrovíz",
        MunicipalityPartName: null,
        Psc: "25261",
        DistrictName: "Praha-západ",
        RegionName: "Středočeský kraj",
        StreetCode: 428582,
        MunicipalityCode: 539171,
        MunicipalityPartCode: null,
        DistrictCode: 3210,
        RegionCode: 27,
        Latitude: 50.1166,
        Longitude: 14.2181,
        Text: "Revoluční 93, 252 61 Dobrovíz");

    private sealed record ProblemDocument(string? Type, string? Title, int Status);

    private sealed class FakeGeocoder : IAddressGeocoder
    {
        public IReadOnlyList<AddressSuggestion> Suggestions { get; set; } = [];
        public ResolvedAddress? Resolved { get; set; }
        public Exception? Throw { get; set; }

        public Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(
            string query, int limit, CancellationToken cancellationToken) =>
            Throw is not null
                ? Task.FromException<IReadOnlyList<AddressSuggestion>>(Throw)
                : Task.FromResult(Suggestions);

        public Task<ResolvedAddress?> ResolveAsync(string key, CancellationToken cancellationToken) =>
            Throw is not null
                ? Task.FromException<ResolvedAddress?>(Throw)
                : Task.FromResult(Resolved);
    }
}
