using System.Net;
using System.Text;
using Ambiquality.Evidence.Api.Infrastructure.Ruian;

namespace Ambiquality.Evidence.Api.Tests.Infrastructure.Ruian;

/// <summary>
/// Verifies the ČÚZK RÚIAN ArcGIS field mapping in isolation, with a stubbed
/// <see cref="HttpMessageHandler"/> returning captured fixture JSON (Revoluční 93, Dobrovíz) for
/// each layer. No network, no database.
/// </summary>
public sealed class RuianGeocoderClientTests
{
    private static RuianGeocoderClient ClientFor(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://ruian.test/") });

    [Fact]
    public async Task SuggestAsync_KeepsOnlyAddressPoints()
    {
        const string json = """
        {"suggestions":[
          {"text":"Revoluční, Praha","magicKey":"1_10","isCollection":false,"type":"Ulice"},
          {"text":"Revoluční 93, 25261 Dobrovíz","magicKey":"1_555742","isCollection":false,"type":"AdresniMisto"}
        ]}
        """;
        var client = ClientFor(new StubHandler(_ => Ok(json)));

        var suggestions = await client.SuggestAsync("Revoluční 93", 10, CancellationToken.None);

        var only = Assert.Single(suggestions);
        Assert.Equal("Revoluční 93, 25261 Dobrovíz", only.Text);
        Assert.Equal("1_555742", only.Key);
    }

    [Fact]
    public async Task SuggestAsync_TooShortQuery_ReturnsEmptyWithoutCallingUpstream()
    {
        var called = false;
        var client = ClientFor(new StubHandler(_ => { called = true; return Ok("{}"); }));

        var suggestions = await client.SuggestAsync("R", 10, CancellationToken.None);

        Assert.Empty(suggestions);
        Assert.False(called);
    }

    [Fact]
    public async Task ResolveAsync_MapsRuianLayersToOfnAddress()
    {
        var client = ClientFor(new StubHandler(DobrovizFixture));

        var resolved = await client.ResolveAsync("1_555742", CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(6265154, resolved!.AddressPointCode);
        Assert.Equal("Revoluční", resolved.StreetName);
        Assert.Equal(428582, resolved.StreetCode);
        Assert.Equal(93, resolved.HouseNumber);
        Assert.Equal("č.p.", resolved.HouseNumberType);
        Assert.Null(resolved.OrientationNumber);
        Assert.Equal("25261", resolved.Psc);
        Assert.Equal("Dobrovíz", resolved.MunicipalityName);
        Assert.Equal(539171, resolved.MunicipalityCode);
        Assert.Null(resolved.MunicipalityPartName); // village has no část obce — empty features
        Assert.Equal("Praha-západ", resolved.DistrictName);
        Assert.Equal(3210, resolved.DistrictCode);
        Assert.Equal("Středočeský kraj", resolved.RegionName);
        Assert.Equal(27, resolved.RegionCode);
        Assert.Equal(50.1166, resolved.Latitude!.Value, 3);
        Assert.Equal(14.2181, resolved.Longitude!.Value, 3);
        Assert.Equal("Revoluční 93, 252 61 Dobrovíz", resolved.Text);
    }

    [Fact]
    public async Task ResolveAsync_NoAddressPoint_ReturnsNull()
    {
        var client = ClientFor(new StubHandler(req =>
            req.RequestUri!.AbsolutePath == "/1/query"
                ? Ok("""{"features":[]}""")
                : Ok("""{"features":[]}""")));

        var resolved = await client.ResolveAsync("1_999999", CancellationToken.None);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_UnparseableKey_ReturnsNull()
    {
        var client = ClientFor(new StubHandler(_ => Ok("{}")));

        Assert.Null(await client.ResolveAsync("not-a-key", CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_UpstreamArcGisError_Throws()
    {
        var client = ClientFor(new StubHandler(req =>
            req.RequestUri!.AbsolutePath == "/1/query"
                ? Ok("""{"error":{"code":500,"message":"boom"}}""")
                : Ok("{}")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ResolveAsync("1_555742", CancellationToken.None));
    }

    private static HttpResponseMessage DobrovizFixture(HttpRequestMessage request) =>
        Ok(request.RequestUri!.AbsolutePath switch
        {
            "/1/query" => """
            {"features":[{"attributes":{"kod":6265154,"cislodomovni":93,"cisloorientacni":null,
              "cisloorientacnipismeno":null,"psc":25261,"ulice":428582},
              "geometry":{"x":14.218078986462098,"y":50.11657939320793}}]}
            """,
            "/4/query" => """{"features":[{"attributes":{"kod":428582,"nazev":"Revoluční"}}]}""",
            "/12/query" => """{"features":[{"attributes":{"kod":539171,"nazev":"Dobrovíz"}}]}""",
            "/11/query" => """{"features":[]}""",
            "/15/query" => """{"features":[{"attributes":{"kod":3210,"nazev":"Praha-západ"}}]}""",
            "/17/query" => """{"features":[{"attributes":{"kod":27,"nazev":"Středočeský kraj"}}]}""",
            _ => "{}",
        });

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
