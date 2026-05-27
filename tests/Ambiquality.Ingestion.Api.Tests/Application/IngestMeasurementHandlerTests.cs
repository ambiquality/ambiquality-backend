using Ambiquality.Core.Domain.Measurements;
using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Ingestion.Api.Application;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Ambiquality.Ingestion.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Ambiquality.Ingestion.Api.Tests.Application;

/// <summary>
/// Branch coverage for the UC10 validation pipeline using an in-memory ieq context
/// and a substituted catalog — fast, no container. The cross-schema SQL and the
/// hypertable insert are covered by the endpoint integration test.
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

    private static IeqDbContext NewIeq()
    {
        var options = new DbContextOptionsBuilder<IeqDbContext>()
            .UseInMemoryDatabase($"ieq-{Guid.NewGuid()}")
            .Options;
        var ctx = new IeqDbContext(options);
        ctx.ParameterRanges.Add(new ParameterRange("co2", 0, 50_000, "ppm"));
        ctx.SaveChanges();
        return ctx;
    }

    private static ISensorCatalog CatalogReturning(SensorValidationView? view)
    {
        var catalog = Substitute.For<ISensorCatalog>();
        catalog.FindSensorAsync(SensorId, Arg.Any<CancellationToken>()).Returns(view);
        return catalog;
    }

    private static IngestMeasurementCommand Command(double value = 800, string parameterCode = "co2") =>
        new(SensorId, PlainKey, parameterCode, value, DateTime.UtcNow);

    [Fact]
    public async Task UnknownSensor_IsUnauthorized()
    {
        var handler = new IngestMeasurementHandler(new FixedClock(DateTime.UtcNow), CatalogReturning(null), NewIeq());

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(IngestRejectionReason.Unauthorized, result.Rejection);
    }

    [Fact]
    public async Task WrongKey_IsUnauthorized()
    {
        var view = new SensorValidationView(SensorKeyHasher.Hash("amq_sk_other"), "active", ["co2"]);
        var handler = new IngestMeasurementHandler(new FixedClock(DateTime.UtcNow), CatalogReturning(view), NewIeq());

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(IngestRejectionReason.Unauthorized, result.Rejection);
    }

    [Fact]
    public async Task InactiveSensor_IsRejected()
    {
        var view = new SensorValidationView(KeyHash, "maintenance", ["co2"]);
        var handler = new IngestMeasurementHandler(new FixedClock(DateTime.UtcNow), CatalogReturning(view), NewIeq());

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(IngestRejectionReason.SensorNotActive, result.Rejection);
    }

    [Fact]
    public async Task UndeclaredParameter_IsRejected()
    {
        var view = new SensorValidationView(KeyHash, "active", ["temperature"]);
        var handler = new IngestMeasurementHandler(new FixedClock(DateTime.UtcNow), CatalogReturning(view), NewIeq());

        var result = await handler.Handle(Command(parameterCode: "co2"), CancellationToken.None);

        Assert.Equal(IngestRejectionReason.ParameterNotDeclared, result.Rejection);
    }

    [Fact]
    public async Task ValueOutOfRange_IsRejected()
    {
        var view = new SensorValidationView(KeyHash, "active", ["co2"]);
        var handler = new IngestMeasurementHandler(new FixedClock(DateTime.UtcNow), CatalogReturning(view), NewIeq());

        var result = await handler.Handle(Command(value: 999_999), CancellationToken.None);

        Assert.Equal(IngestRejectionReason.ValueOutOfRange, result.Rejection);
    }

    [Fact]
    public async Task ValidObservation_IsAcceptedAndPersisted()
    {
        var view = new SensorValidationView(KeyHash, "active", ["co2"]);
        var ieq = NewIeq();
        var now = new DateTime(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);
        var handler = new IngestMeasurementHandler(new FixedClock(now), CatalogReturning(view), ieq);

        var result = await handler.Handle(Command(value: 800), CancellationToken.None);

        Assert.True(result.IsAccepted);
        Assert.Equal(now, result.ReceivedAt);
        var stored = Assert.Single(ieq.Measurements);
        Assert.Equal(SensorId, stored.SensorId);
        Assert.Equal("co2", stored.ParameterCode);
        Assert.Equal(800, stored.Value);
        Assert.Equal(now, stored.ReceivedAt);
        Assert.False(stored.IsInvalid);
    }
}
