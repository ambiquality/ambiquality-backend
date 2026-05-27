extern alias evidence;
using Ambiquality.Core.Infrastructure.Persistence;
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

    public async Task InitializeAsync() => await _postgres.InitializeAsync();

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
            "Seed Building", Address.Create("1 Main St", "Prague", "11000", "CZ"),
            "HOUSE", Coordinates.Create(50.0, 14.0), AnonymizationLevel.Precise,
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

    public async Task<int> CountMeasurementsAsync(Guid sensorId)
    {
        var options = new DbContextOptionsBuilder<IeqDbContext>()
            .UseNpgsql(_postgres.ConnectionString).Options;
        await using var db = new IeqDbContext(options);
        return await db.Measurements.CountAsync(m => m.SensorId == sensorId);
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
