using Ambiquality.Evidence.Api;
using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Infrastructure.Persistence;
using Ambiquality.Evidence.Api.Tests.TestSupport;
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

            // Replace ICurrentUser with test stub
            services.AddScoped<ICurrentUser>(_ =>
                new StubCurrentUser(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Guid.Parse("22222222-2222-2222-2222-222222222222")));
        });
    }
}
