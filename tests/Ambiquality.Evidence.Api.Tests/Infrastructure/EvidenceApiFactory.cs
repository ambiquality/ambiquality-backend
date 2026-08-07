using Ambiquality.Evidence.Api;
using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Infrastructure.Persistence;
using Ambiquality.Evidence.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ambiquality.Evidence.Api.Tests.Infrastructure;

public sealed class EvidenceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

    /// <summary>
    /// Optional fake <see cref="IAddressGeocoder"/> swapped in for the real ČÚZK HTTP client, so
    /// address-lookup endpoint tests run without reaching the external RÚIAN service.
    /// </summary>
    public IAddressGeocoder? Geocoder { get; init; }

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // No fixed-port metrics listener in tests.
        builder.UseSetting("Observability:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            // Remove the default DbContext registration
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<EvidenceDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            // Replace with test container connection
            services.AddDbContext<EvidenceDbContext>(options =>
                options.UseNpgsql(_postgres.ConnectionString,
                    o => o.MigrationsHistoryTable("__EFMigrationsHistory", "evidence")));

            // Swap real JWT bearer validation for a test scheme that authenticates
            // from request headers. The real CurrentUser middleware still runs and
            // upserts a UserProjection, so ownership behaves as in production.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Replace the real RÚIAN HTTP client with a fake when a test supplies one.
            if (Geocoder is not null)
            {
                services.RemoveAll<IAddressGeocoder>();
                services.AddSingleton(Geocoder);
            }
        });
    }
}
