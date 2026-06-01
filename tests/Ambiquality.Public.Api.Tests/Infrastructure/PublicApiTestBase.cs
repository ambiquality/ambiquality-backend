namespace Ambiquality.Public.Api.Tests.Infrastructure;

/// <summary>Base for endpoint tests: a fresh factory + client over the shared seeded container.</summary>
[Collection("Public API")]
public abstract class PublicApiTestBase : IDisposable
{
    protected readonly PublicApiFactory Factory;
    protected readonly HttpClient Client;

    protected PublicApiTestBase(TimescaleFixture fixture)
    {
        Factory = new PublicApiFactory(fixture.ConnectionString);
        Client = Factory.CreateClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
