using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Evidence.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Ambiquality.Public.Api.Tests.Infrastructure;

/// <summary>
/// Starts a single TimescaleDB container hosting both the <c>evidence</c> and
/// <c>ieq</c> schemas (production keeps them in separate databases; one database
/// with two schemas is equivalent for the schema-qualified read queries). The
/// TimescaleDB image is required because the ieq initial migration creates a
/// hypertable; <c>shared_preload_libraries=timescaledb</c> mirrors production.
/// </summary>
public sealed class TimescaleFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not initialized");

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("timescale/timescaledb:2.27.0-pg18")
            .WithDatabase("public_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithEnvironment("POSTGRES_INITDB_ARGS", "-c shared_preload_libraries=timescaledb")
            .Build();

        await _container.StartAsync();

        // Evidence schema + tables (migrations live in Evidence.Api).
        await MigrateAsync<EvidenceDbContext>(options => options.UseNpgsql(
            ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "evidence")));

        // ieq schema + measurements hypertable (migrations live in Ingestion.Api).
        await MigrateAsync<IeqDbContext>(options => options.UseNpgsql(
            ConnectionString, npgsql => npgsql
                .MigrationsAssembly("Ambiquality.Ingestion.Api")
                .MigrationsHistoryTable("__EFMigrationsHistory", "ieq")));

        // One shared, read-only dataset for the whole run.
        await EvidenceSeed.SeedAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    private async Task MigrateAsync<TContext>(Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        var services = new ServiceCollection();
        services.AddDbContext<TContext>(configure);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync();
    }
}

/// <summary>Shares one container across the whole test run.</summary>
[CollectionDefinition("Public API")]
public sealed class PublicApiCollection : ICollectionFixture<TimescaleFixture>;
