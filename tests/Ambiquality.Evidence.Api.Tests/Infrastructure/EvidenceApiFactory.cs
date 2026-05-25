using Ambiquality.Evidence.Api;
using Ambiquality.Evidence.Api.Infrastructure.Persistence;
using Ambiquality.Evidence.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ambiquality.Evidence.Api.Tests.Infrastructure;

public sealed class EvidenceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

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
        });
    }
}
