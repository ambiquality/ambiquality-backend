extern alias evidence;
using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Ambiquality.Ingestion.Api.Infrastructure.Catalog;
using Ambiquality.Ingestion.Api.Infrastructure.Security;
using EvidenceDb = evidence::Ambiquality.Evidence.Api.Infrastructure.Persistence.EvidenceDbContext;
using evidence::Ambiquality.Evidence.Api.Domain.Buildings;
using evidence::Ambiquality.Evidence.Api.Domain.Common;
using evidence::Ambiquality.Evidence.Api.Domain.Rooms;
using evidence::Ambiquality.Evidence.Api.Domain.Sensors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ambiquality.Ingestion.Api.Tests.Infrastructure;

public sealed class IngestionApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly IngestionPostgresFixture _postgres = new();

    /// <summary>Captures what the endpoint enqueued, replacing the real Redis publisher.</summary>
    public CapturingQueuePublisher Queue { get; } = new();

    /// <summary>In-memory rate limiter, replacing the Redis-backed one (no broker in tests).</summary>
    public InMemoryFixedWindowRateLimiter RateLimiter { get; } = new();

    public async Task InitializeAsync() => await _postgres.InitializeAsync();

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
            var ieqDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<IeqDbContext>));
            if (ieqDescriptor is not null)
                services.Remove(ieqDescriptor);

            services.AddDbContext<IeqDbContext>(options =>
                options.UseNpgsql(_postgres.ConnectionString,
                    o => o.MigrationsHistoryTable("__EFMigrationsHistory", "ieq")
                          .MigrationsAssembly("Ambiquality.Ingestion.Api")));

            var catalogDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ISensorCatalog));
            if (catalogDescriptor is not null)
                services.Remove(catalogDescriptor);

            services.AddSingleton<ISensorCatalog>(
                _ => new SensorCatalog(NpgsqlDataSource.Create(_postgres.ConnectionString)));

            // Replace the Redis publisher with an in-memory capture so the endpoint
            // tests need no broker; removing it also means the lazy IConnectionMultiplexer
            // is never resolved (no socket opened).
            var publisherDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IMeasurementQueuePublisher));
            if (publisherDescriptor is not null)
                services.Remove(publisherDescriptor);

            services.AddSingleton<IMeasurementQueuePublisher>(Queue);

            var limiterDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRateLimiter));
            if (limiterDescriptor is not null)
                services.Remove(limiterDescriptor);

            services.AddSingleton<IRateLimiter>(RateLimiter);
        });
    }

    /// <summary>
    /// Seeds a building → room → sensor into the evidence catalog and returns the
    /// sensor id with its plaintext API key (hashed the same way Evidence stores it).
    /// </summary>
    public async Task<(Guid SensorId, string ApiKey)> SeedSensorAsync(
        string[] parameterCodes, string statusCode = "active")
    {
        var now = DateTime.UtcNow;
        var owner = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var building = Building.Register(
            UriSlug.Create($"seed-building-{suffix}"), owner, owner,
            "Seed Building", Address.Create(10000001, "Hlavní", 1, "č.p.", null, null, "Praha", null, "11000", null, null),
            "HOUSE", Coordinates.Create(50.0, 14.0),
            yearBuilt: 2000, yearRenovated: null, now);

        var room = Room.Register(
            UriSlug.Create($"seed-room-{suffix}"), building.Id, owner,
            "Seed Room", FloorNumber.Create(1),
            functionCode: null, exposureCode: null, areaM2: null, ceilingHeightM: null,
            ventilationType: null, pollutionSources: Array.Empty<string>(), now);

        var plainKey = $"amq_sk_{Guid.NewGuid():N}";
        var sensor = Sensor.Register(
            UriSlug.Create($"seed-sensor-{suffix}"), building.Id, room.Id, owner,
            "Aranet", "Aranet4", $"SN-{suffix}",
            SensorStatus.FromCode(statusCode),
            parameterCodes.Select(MeasuredParameter.FromCode).ToList(),
            apiKeyHash: SensorKeyHasher.Hash(plainKey), now);

        await using var db = NewEvidenceContext();
        db.Add(building);
        db.Add(room);
        db.Add(sensor);
        await db.SaveChangesAsync();

        return (sensor.Id, plainKey);
    }

    private EvidenceDb NewEvidenceContext()
    {
        var options = new DbContextOptionsBuilder<EvidenceDb>()
            .UseNpgsql(_postgres.ConnectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "evidence"))
            .Options;
        return new EvidenceDb(options);
    }
}
