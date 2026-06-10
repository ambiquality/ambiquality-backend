using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Api.Infrastructure.Queue;
using Ambiquality.Ingestion.Worker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Ambiquality.Ingestion.Worker.Tests;

/// <summary>
/// Full path: the API's Redis publisher appends to the stream, the worker's drain
/// service consumes the consumer group and the batch writer materializes rows into
/// TimescaleDB. Validates the XADD/XREADGROUP/XACK wiring and that a redelivered
/// measurement id produces exactly one row (exactly-once effect).
/// </summary>
public sealed class IngestionQueueEndToEndTests : IAsyncLifetime
{
    private readonly IeqPostgresFixture _postgres = new();
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private NpgsqlDataSource _dataSource = null!;
    private IConnectionMultiplexer _redisMux = null!;
    private readonly MeasurementQueueOptions _options = new()
    {
        StreamKey = "ieq:measurements:test",
        ConsumerGroup = "writers",
        BatchSize = 100,
        BlockMilliseconds = 200,
    };

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        await _redis.StartAsync();
        _dataSource = NpgsqlDataSource.Create(_postgres.ConnectionString);
        _redisMux = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _redisMux.DisposeAsync();
        await _dataSource.DisposeAsync();
        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task PublishedMeasurements_AreDrainedIntoTimescale_ExactlyOnce()
    {
        var publisher = new RedisMeasurementQueuePublisher(_redisMux, Options.Create(_options));
        var writer = new MeasurementBatchWriter(_dataSource);
        var drain = new MeasurementDrainService(
            _redisMux, writer, Options.Create(_options),
            NullLogger<MeasurementDrainService>.Instance);

        var first = Measurement(700);
        var second = Measurement(800);

        await publisher.PublishAsync([first], CancellationToken.None);
        await publisher.PublishAsync([second], CancellationToken.None);
        // Same measurement id appended again (e.g. a producer retry): must not duplicate.
        await publisher.PublishAsync([first], CancellationToken.None);

        await drain.StartAsync(CancellationToken.None);
        try
        {
            await WaitForCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(20));
        }
        finally
        {
            await drain.StopAsync(CancellationToken.None);
        }

        await using var db = _postgres.NewContext();
        Assert.Equal(1, await db.Measurements.CountAsync(m => m.Id == first.Id));
        Assert.Equal(1, await db.Measurements.CountAsync(m => m.Id == second.Id));
    }

    [Fact]
    public async Task PublishedBatch_LandsAtomically_AndIsDrainedInFull()
    {
        var publisher = new RedisMeasurementQueuePublisher(_redisMux, Options.Create(_options));
        var writer = new MeasurementBatchWriter(_dataSource);
        var drain = new MeasurementDrainService(
            _redisMux, writer, Options.Create(_options),
            NullLogger<MeasurementDrainService>.Instance);

        // A multi-reading batch is appended inside one MULTI/EXEC transaction.
        var batch = new[] { Measurement(700), Measurement(800), Measurement(900) };

        await publisher.PublishAsync(batch, CancellationToken.None);

        await drain.StartAsync(CancellationToken.None);
        try
        {
            await WaitForCountAsync(expected: 3, timeout: TimeSpan.FromSeconds(20));
        }
        finally
        {
            await drain.StopAsync(CancellationToken.None);
        }

        await using var db = _postgres.NewContext();
        foreach (var message in batch)
            Assert.Equal(1, await db.Measurements.CountAsync(m => m.Id == message.Id));
    }

    private async Task WaitForCountAsync(int expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = _postgres.NewContext();
            if (await db.Measurements.CountAsync() >= expected)
                return;
            await Task.Delay(250);
        }

        await using var final = _postgres.NewContext();
        Assert.Equal(expected, await final.Measurements.CountAsync());
    }

    private static MeasurementMessage Measurement(double value) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "co2", value, "ppm",
            new DateTime(2026, 5, 28, 8, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
}
