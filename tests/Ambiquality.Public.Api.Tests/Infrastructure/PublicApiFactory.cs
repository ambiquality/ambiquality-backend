extern alias PublicApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ambiquality.Public.Api.Tests.Infrastructure;

/// <summary>
/// Boots Public.Api against the shared test container. Both connection strings and
/// a fixed base IRI are injected via configuration, so the read-only DbContext and
/// the evidence catalog singleton pick them up without any service re-registration.
/// No auth handler is needed — Public.Api is unauthenticated.
/// </summary>
public sealed class PublicApiFactory(string connectionString) : WebApplicationFactory<PublicApi::Program>
{
    public const string BaseIri = "https://data.test.example";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // No fixed-port metrics listener in tests.
        builder.UseSetting("Observability:Enabled", "false");
        builder.UseSetting("ConnectionStrings:IeqDb", connectionString);
        builder.UseSetting("ConnectionStrings:EvidenceDb", connectionString);
        builder.UseSetting("PublicApi:BaseIri", BaseIri);
    }
}
