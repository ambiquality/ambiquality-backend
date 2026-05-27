extern alias evidence;
using Ambiquality.Core.Infrastructure.Persistence;
using EvidenceDb = evidence::Ambiquality.Evidence.Api.Infrastructure.Persistence.EvidenceDbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Ambiquality.Ingestion.Api.Tests.Infrastructure;

/// <summary>
/// One TimescaleDB container hosting both schemas the ingestion service touches:
/// <c>evidence</c> (the catalog it validates against) and <c>ieq</c> (the
/// measurements hypertable). The catalog queries are schema-qualified, so a single
/// database with two schemas behaves like production's two databases. The
/// timescale image is required so the ieq migration's <c>create_hypertable</c> runs.
/// </summary>
public sealed class IngestionPostgresFixture : IAsyncLifetime
{
    private const string IngestionMigrationsAssembly = "Ambiquality.Ingestion.Api";

    private PostgreSqlContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not initialized");

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("timescale/timescaledb:2.27.0-pg18")
            .WithDatabase("ambiquality_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithEnvironment("POSTGRES_INITDB_ARGS", "-c shared_preload_libraries=timescaledb")
            .Build();

        await _container.StartAsync();

        // Evidence catalog first (the ieq side has no FK to it, but order is harmless).
        await MigrateAsync<EvidenceDb>("evidence", migrationsAssembly: null);
        await MigrateAsync<IeqDbContext>("ieq", migrationsAssembly: IngestionMigrationsAssembly);
    }

    private async Task MigrateAsync<TContext>(string historySchema, string? migrationsAssembly)
        where TContext : DbContext
    {
        var services = new ServiceCollection();
        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", historySchema);
                if (migrationsAssembly is not null)
                    npgsql.MigrationsAssembly(migrationsAssembly);
            }));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }
}
