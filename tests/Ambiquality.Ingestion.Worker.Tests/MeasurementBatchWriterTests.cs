using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Worker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ambiquality.Ingestion.Worker.Tests;

/// <summary>
/// Integration coverage for the worker's write path against a real TimescaleDB
/// hypertable: rows land with their fields intact (especially the acceptance
/// timestamp), and redelivered messages are skipped rather than duplicated.
/// </summary>
public sealed class MeasurementBatchWriterTests : IAsyncLifetime
{
    private readonly IeqPostgresFixture _postgres = new();
    private NpgsqlDataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _dataSource = NpgsqlDataSource.Create(_postgres.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private static MeasurementMessage Message(Guid? id = null, double value = 800, DateTime? receivedAt = null) =>
        new(
            Id: id ?? Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            ParameterCode: "co2",
            Value: value,
            Unit: "ppm",
            ObservedAt: new DateTime(2026, 5, 28, 8, 0, 0, DateTimeKind.Utc),
            ReceivedAt: receivedAt ?? DateTime.UtcNow);

    [Fact]
    public async Task WriteAsync_PersistsRows_WithReceivedAtPreservedExactly()
    {
        var writer = new MeasurementBatchWriter(_dataSource);
        // Microsecond precision: the hypertable's timestamptz column stores µs, so the
        // acceptance timestamp is preserved to that resolution (far finer than sensor
        // cadence). The queue itself carries full 100ns ticks — see the serializer test.
        var receivedAt = new DateTime(2026, 5, 28, 9, 30, 16, 123, DateTimeKind.Utc).AddTicks(4560);
        var message = Message(value: 812, receivedAt: receivedAt);

        var inserted = await writer.WriteAsync([message], CancellationToken.None);

        Assert.Equal(1, inserted);
        await using var db = _postgres.NewContext();
        var stored = await db.Measurements.SingleAsync(m => m.Id == message.Id);
        Assert.Equal(message.SensorId, stored.SensorId);
        Assert.Equal("co2", stored.ParameterCode);
        Assert.Equal(812, stored.Value);
        Assert.Equal("ppm", stored.Unit);
        Assert.Equal(message.ObservedAt, stored.ObservedAt);
        Assert.Equal(receivedAt.Ticks, stored.ReceivedAt.Ticks);
        Assert.False(stored.IsInvalid);
    }

    [Fact]
    public async Task WriteAsync_IsIdempotent_OnRedelivery()
    {
        var writer = new MeasurementBatchWriter(_dataSource);
        var batch = new[] { Message(), Message() };

        var first = await writer.WriteAsync(batch, CancellationToken.None);
        var second = await writer.WriteAsync(batch, CancellationToken.None);

        Assert.Equal(2, first);
        Assert.Equal(0, second);

        await using var db = _postgres.NewContext();
        foreach (var m in batch)
            Assert.Equal(1, await db.Measurements.CountAsync(x => x.Id == m.Id));
    }

    [Fact]
    public async Task WriteAsync_OverlappingBatch_InsertsOnlyNewRows()
    {
        var writer = new MeasurementBatchWriter(_dataSource);
        var a = Message();
        var b = Message();
        var c = Message();

        await writer.WriteAsync([a, b], CancellationToken.None);
        var inserted = await writer.WriteAsync([b, c], CancellationToken.None);

        Assert.Equal(1, inserted);
        await using var db = _postgres.NewContext();
        Assert.Equal(1, await db.Measurements.CountAsync(x => x.Id == c.Id));
    }
}
