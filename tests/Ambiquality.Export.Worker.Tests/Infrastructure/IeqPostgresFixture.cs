using Ambiquality.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Ambiquality.Export.Worker.Tests.Infrastructure;

/// <summary>
/// A TimescaleDB container with the <c>ieq</c> schema migrated (the migration runs
/// <c>create_hypertable</c> and creates <c>measurement_exports</c>). The export worker
/// only touches <c>ieq</c>, so no evidence schema is provisioned.
/// </summary>
public sealed class IeqPostgresFixture : IAsyncLifetime
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

        var services = new ServiceCollection();
        services.AddDbContext<IeqDbContext>(options =>
            options.UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ieq");
                npgsql.MigrationsAssembly(IngestionMigrationsAssembly);
            }));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IeqDbContext>();
        await context.Database.MigrateAsync();
    }

    public IeqDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<IeqDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new IeqDbContext(options);
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
