using Ambiquality.Core.Domain.Measurements;
using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Api.Application;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Ambiquality.Ingestion.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Ambiquality.Ingestion.Api.Tests.Application;

/// <summary>
/// Branch coverage for the UC10 validation pipeline using an in-memory ieq context
/// (for parameter-range reads), a substituted catalog and a fake queue — fast, no
/// container. The handler validates then enqueues; the durable write and the
/// cross-schema SQL are covered by the worker and endpoint integration tests.
/// </summary>
public class IngestMeasurementHandlerTests
{
    private static readonly Guid SensorId = Guid.NewGuid();
    private const string PlainKey = "amq_sk_unit_test_key";
    private static readonly string KeyHash = SensorKeyHasher.Hash(PlainKey);

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }

    /// <summary>Captures what was enqueued; optionally simulates an unreachable queue.</summary>
    private sealed class FakeQueue : IMeasurementQueuePublisher
    {
        private readonly bool _throws;
        public FakeQueue(bool throws = false) => _throws = throws;
        public List<MeasurementMessage> Published { get; } = [];

        public Task PublishAsync(IReadOnlyList<MeasurementMessage> messages, CancellationToken cancellationToken)
        {
            if (_throws)
                throw new InvalidOperationException("queue down");
            Published.AddRange(messages);
            return Task.CompletedTask;
        }
    }

    private static IeqDbContext NewIeq()
    {
        var options = new DbContextOptionsBuilder<IeqDbContext>()
            .UseInMemoryDatabase($"ieq-{Guid.NewGuid()}")
            .Options;
        var ctx = new IeqDbContext(options);
        ctx.ParameterRanges.Add(new ParameterRange("co2", 0, 50_000, "ppm"));
        ctx.ParameterRanges.Add(new ParameterRange("temperature", -40, 60, "Cel"));
        ctx.ParameterRanges.Add(new ParameterRange("humidity", 0, 100, "%"));
        ctx.SaveChanges();
        return ctx;
    }

    private static ISensorCatalog CatalogReturning(SensorValidationView? view)
    {
        var catalog = Substitute.For<ISensorCatalog>();
        catalog.FindSensorAsync(SensorId, Arg.Any<CancellationToken>()).Returns(view);
        return catalog;
    }

    private static IngestMeasurementsCommand Command(double value = 800, string parameterCode = "co2") =>
        new(SensorId, PlainKey, [new MeasurementReadingInput(parameterCode, value)]);

    private static IngestMeasurementsCommand Batch(params MeasurementReadingInput[] readings) =>
        new(SensorId, PlainKey, readings);

    private static IngestMeasurementHandler Handler(
        SensorValidationView? view, FakeQueue queue, DateTime? now = null) =>
        new(new FixedClock(now ?? DateTime.UtcNow), CatalogReturning(view), NewIeq(), queue);

    [Fact]
    public async Task UnknownSensor_IsUnauthorized_AndNotEnqueued()
    {
        var queue = new FakeQueue();
        var handler = Handler(view: null, queue);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(IngestRejectionReason.Unauthorized, result.Rejection);
        Assert.Empty(queue.Published);
    }

    [Fact]
    public async Task WrongKey_IsUnauthorized_AndNotEnqueued()
    {
        var queue = new FakeQueue();
        var view = new SensorValidationView(SensorKeyHasher.Hash("amq_sk_other"), "active", ["co2"]);
        var handler = Handler(view, queue);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(IngestRejectionReason.Unauthorized, result.Rejection);
        Assert.Empty(queue.Published);
    }

    [Fact]
    public async Task InactiveSensor_IsRejected_AndNotEnqueued()
    {
        var queue = new FakeQueue();
        var view = new SensorValidationView(KeyHash, "maintenance", ["co2"]);
        var handler = Handler(view, queue);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(IngestRejectionReason.SensorNotActive, result.Rejection);
        Assert.Empty(queue.Published);
    }

    [Fact]
    public async Task UndeclaredParameter_IsRejected_AndNotEnqueued()
    {
        var queue = new FakeQueue();
        var view = new SensorValidationView(KeyHash, "active", ["temperature"]);
        var handler = Handler(view, queue);

        var result = await handler.Handle(Command(parameterCode: "co2"), CancellationToken.None);

        Assert.Equal(IngestRejectionReason.ParameterNotDeclared, result.Rejection);
        Assert.Empty(queue.Published);
    }

    [Fact]
    public async Task ValueOutOfRange_IsRejected_AndNotEnqueued()
    {
        var queue = new FakeQueue();
        var view = new SensorValidationView(KeyHash, "active", ["co2"]);
        var handler = Handler(view, queue);

        var result = await handler.Handle(Command(value: 999_999), CancellationToken.None);

        Assert.Equal(IngestRejectionReason.ValueOutOfRange, result.Rejection);
        Assert.Empty(queue.Published);
    }

    [Fact]
    public async Task ValidObservation_IsEnqueued_WithReceivedAtStampedAtAcceptance()
    {
        var queue = new FakeQueue();
        var view = new SensorValidationView(KeyHash, "active", ["co2"]);
        var now = new DateTime(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);
        var handler = Handler(view, queue, now);

        var result = await handler.Handle(Command(value: 800), CancellationToken.None);

        Assert.True(result.IsAccepted);
        Assert.Equal(now, result.ReceivedAt);

        var accepted = Assert.Single(result.Accepted!);
        var message = Assert.Single(queue.Published);
        Assert.Equal(accepted.Id, message.Id);
        Assert.Equal("co2", accepted.ParameterCode);
        Assert.Equal(SensorId, message.SensorId);
        Assert.Equal("co2", message.ParameterCode);
        Assert.Equal(800, message.Value);
        Assert.Equal(now, message.ReceivedAt);
        // ObservedAt is server-stamped (sensor clock untrusted), so it equals ReceivedAt.
        Assert.Equal(now, message.ObservedAt);
    }

    [Fact]
    public async Task MultiReadingBatch_EnqueuesEveryReading_WithDistinctIdsAndOneReceivedAt()
    {
        var queue = new FakeQueue();
        var view = new SensorValidationView(KeyHash, "active", ["co2", "temperature", "humidity"]);
        var now = new DateTime(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);
        var handler = Handler(view, queue, now);

        var result = await handler.Handle(
            Batch(
                new MeasurementReadingInput("co2", 800),
                new MeasurementReadingInput("temperature", 21.5),
                new MeasurementReadingInput("humidity", 45)),
            CancellationToken.None);

        Assert.True(result.IsAccepted);
        Assert.Equal(3, queue.Published.Count);
        Assert.Equal(3, result.Accepted!.Count);
        // Every reading shares the single acceptance timestamp...
        Assert.All(queue.Published, m => Assert.Equal(now, m.ReceivedAt));
        // ...but each carries its own measurement identity.
        Assert.Equal(3, queue.Published.Select(m => m.Id).Distinct().Count());
        Assert.Equal(
            ["co2", "temperature", "humidity"],
            queue.Published.Select(m => m.ParameterCode));
    }

    [Fact]
    public async Task EmptyBatch_IsRejected_AndNotEnqueued()
    {
        var queue = new FakeQueue();
        var view = new SensorValidationView(KeyHash, "active", ["co2"]);
        var handler = Handler(view, queue);

        var result = await handler.Handle(
            new IngestMeasurementsCommand(SensorId, PlainKey, []), CancellationToken.None);

        Assert.Equal(IngestRejectionReason.EmptyBatch, result.Rejection);
        Assert.Empty(queue.Published);
    }

    [Fact]
    public async Task DuplicateParameterInBatch_IsRejected_AndNotEnqueued()
    {
        var queue = new FakeQueue();
        var view = new SensorValidationView(KeyHash, "active", ["co2"]);
        var handler = Handler(view, queue);

        var result = await handler.Handle(
            Batch(
                new MeasurementReadingInput("co2", 800),
                new MeasurementReadingInput("co2", 810)),
            CancellationToken.None);

        Assert.Equal(IngestRejectionReason.DuplicateParameter, result.Rejection);
        Assert.Empty(queue.Published);
    }

    [Fact]
    public async Task OneBadReadingInBatch_RejectsWholeBatch_AndEnqueuesNothing()
    {
        var queue = new FakeQueue();
        var view = new SensorValidationView(KeyHash, "active", ["co2", "temperature"]);
        var handler = Handler(view, queue);

        // temperature is valid, co2 is out of range — the whole batch must be rejected.
        var result = await handler.Handle(
            Batch(
                new MeasurementReadingInput("temperature", 21.5),
                new MeasurementReadingInput("co2", 999_999)),
            CancellationToken.None);

        Assert.Equal(IngestRejectionReason.ValueOutOfRange, result.Rejection);
        Assert.Empty(queue.Published);
    }

    [Fact]
    public async Task QueueUnreachable_IsRejectedAsQueueUnavailable()
    {
        var queue = new FakeQueue(throws: true);
        var view = new SensorValidationView(KeyHash, "active", ["co2"]);
        var handler = Handler(view, queue);

        var result = await handler.Handle(Command(value: 800), CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(IngestRejectionReason.QueueUnavailable, result.Rejection);
    }
}
