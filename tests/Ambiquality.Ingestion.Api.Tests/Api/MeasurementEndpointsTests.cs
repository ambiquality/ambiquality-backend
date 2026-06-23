using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Ingestion.Api.Api;
using Ambiquality.Ingestion.Api.Tests.Infrastructure;

namespace Ambiquality.Ingestion.Api.Tests.Api;

public sealed class MeasurementEndpointsTests : IAsyncLifetime
{
    private IngestionApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new IngestionApiFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>Canonical units from the real `parameter_ranges` seed the fixture migrates.</summary>
    private static string UnitFor(string parameterCode) => parameterCode switch
    {
        "co2" => "ppm",
        "temperature" => "°C",
        "humidity" => "%",
        _ => throw new ArgumentOutOfRangeException(nameof(parameterCode)),
    };

    private Task<HttpResponseMessage> PostAsync(
        Guid sensorId, string parameterCode, double value, string? apiKey) =>
        PostReadingsAsync(sensorId, apiKey, new { parameterCode, value, unit = UnitFor(parameterCode) });

    private async Task<HttpResponseMessage> PostReadingsAsync(
        Guid sensorId, string? apiKey, params object[] readings)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/measurements")
        {
            Content = JsonContent.Create(new
            {
                sensorId,
                readings,
            }),
        };
        if (apiKey is not null)
            request.Headers.Add(MeasurementEndpoints.SensorKeyHeader, apiKey);

        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task ValidObservation_Returns202AndEnqueues()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2", "temperature"]);

        var response = await PostAsync(sensorId, "co2", 812, apiKey);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeasurementsAcceptedResponse>();
        var accepted = Assert.Single(body!.Measurements);
        Assert.NotEqual(Guid.Empty, accepted.Id);
        Assert.Equal("co2", accepted.ParameterCode);

        var message = Assert.Single(_factory.Queue.Published);
        Assert.Equal(accepted.Id, message.Id);
        Assert.Equal(sensorId, message.SensorId);
        Assert.Equal("co2", message.ParameterCode);
        Assert.Equal(body.ReceivedAt, message.ReceivedAt);
        // ObservedAt is server-stamped, not taken from the sensor.
        Assert.Equal(body.ReceivedAt, message.ObservedAt);
    }

    [Fact]
    public async Task BatchOfReadings_Returns202AndEnqueuesEach()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2", "temperature", "humidity"]);

        var response = await PostReadingsAsync(
            sensorId, apiKey,
            new { parameterCode = "co2", value = 812.0, unit = "ppm" },
            new { parameterCode = "temperature", value = 21.5, unit = "°C" },
            new { parameterCode = "humidity", value = 45.0, unit = "%" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeasurementsAcceptedResponse>();
        Assert.Equal(3, body!.Measurements.Count);
        Assert.Equal(3, _factory.Queue.Published.Count);
        // One acceptance timestamp shared by the batch; one distinct id per reading.
        Assert.All(_factory.Queue.Published, m => Assert.Equal(body.ReceivedAt, m.ReceivedAt));
        Assert.Equal(3, _factory.Queue.Published.Select(m => m.Id).Distinct().Count());
        Assert.Equal(
            ["co2", "temperature", "humidity"],
            _factory.Queue.Published.Select(m => m.ParameterCode));
    }

    [Fact]
    public async Task BatchWithOneBadReading_Returns422_AndEnqueuesNothing()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2", "temperature"]);

        // temperature is valid, co2 is out of range — atomic batch must reject the whole request.
        var response = await PostReadingsAsync(
            sensorId, apiKey,
            new { parameterCode = "temperature", value = 21.5, unit = "°C" },
            new { parameterCode = "co2", value = 999_999.0, unit = "ppm" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(_factory.Queue.Published);
    }

    [Fact]
    public async Task EmptyBatch_Returns422_AndEnqueuesNothing()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostReadingsAsync(sensorId, apiKey);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(_factory.Queue.Published);
    }

    [Fact]
    public async Task ClientSuppliedObservedAt_IsIgnored_AndServerStampsTheTime()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        // A sensor with a badly skewed clock (or a malicious client) tries to dictate
        // observedAt. The API must ignore the body field and stamp the time itself.
        var skewed = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var before = DateTime.UtcNow;
        var response = await PostReadingsAsync(
            sensorId, apiKey,
            new { parameterCode = "co2", value = 800.0, unit = "ppm", observedAt = skewed });
        var after = DateTime.UtcNow;

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var message = Assert.Single(_factory.Queue.Published);
        Assert.NotEqual(skewed, message.ObservedAt);
        Assert.InRange(message.ObservedAt, before, after);
        Assert.Equal(message.ReceivedAt, message.ObservedAt);
    }

    [Fact]
    public async Task QueueOutage_Returns503AndAcksNothing()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);
        _factory.Queue.Fail = true;

        var response = await PostAsync(sensorId, "co2", 800, apiKey);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(_factory.Queue.Published);
    }

    [Fact]
    public async Task MissingKey_Returns401_AndEnqueuesNothing()
    {
        var (sensorId, _) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostAsync(sensorId, "co2", 800, apiKey: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Queue.Published);
    }

    [Fact]
    public async Task WrongKey_Returns401()
    {
        var (sensorId, _) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostAsync(sensorId, "co2", 800, apiKey: "amq_sk_not_the_key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownSensor_Returns401()
    {
        var response = await PostAsync(Guid.NewGuid(), "co2", 800, apiKey: "amq_sk_whatever");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InactiveSensor_Returns403_AndEnqueuesNothing()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"], statusCode: "maintenance");

        var response = await PostAsync(sensorId, "co2", 800, apiKey);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_factory.Queue.Published);
    }

    [Fact]
    public async Task UndeclaredParameter_Returns422_AndEnqueuesNothing()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostAsync(sensorId, "humidity", 50, apiKey);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(_factory.Queue.Published);
    }

    [Fact]
    public async Task WrongUnit_Returns422WithUnitMismatchType_AndEnqueuesNothing()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostReadingsAsync(
            sensorId, apiKey, new { parameterCode = "co2", value = 812.0, unit = "mg/m³" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(_factory.Queue.Published);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "urn:ambiquality:ingestion:unit-mismatch",
            problem.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task MissingUnit_Returns422_AndEnqueuesNothing()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostReadingsAsync(
            sensorId, apiKey, new { parameterCode = "co2", value = 812.0 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(_factory.Queue.Published);
    }

    [Fact]
    public async Task AcceptedReading_CarriesCanonicalUnitOntoTheQueue()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostAsync(sensorId, "co2", 812, apiKey);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var message = Assert.Single(_factory.Queue.Published);
        Assert.Equal("ppm", message.Unit);
    }

    [Fact]
    public async Task SecondBatchWithinWindow_Returns429WithRetryAfter_AndEnqueuesOnlyTheFirst()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        var first = await PostAsync(sensorId, "co2", 800, apiKey);
        var second = await PostAsync(sensorId, "co2", 810, apiKey);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);

        // Only the first reading made it onto the queue.
        var message = Assert.Single(_factory.Queue.Published);
        Assert.Equal(800, message.Value);

        // 429 carries Retry-After (seconds left in the 5-minute default window) and the
        // uniform problem type.
        Assert.NotNull(second.Headers.RetryAfter?.Delta);
        Assert.InRange(second.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 300);
        using var problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(
            "urn:ambiquality:ingestion:rate-limited",
            problem.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task BatchAfterWindowElapses_IsAcceptedAgain()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        var first = await PostAsync(sensorId, "co2", 800, apiKey);
        // Jump past the 5-minute default window so the sensor's counter resets.
        _factory.RateLimiter.Clock = () => DateTimeOffset.UtcNow.AddSeconds(301);
        var second = await PostAsync(sensorId, "co2", 810, apiKey);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(2, _factory.Queue.Published.Count);
    }

    [Fact]
    public async Task ValueOutOfRange_Returns422_AndEnqueuesNothing()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostAsync(sensorId, "co2", 999_999, apiKey);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(_factory.Queue.Published);

        // Every problem carries the urn:ambiquality:ingestion:<reason> type, uniform
        // with the other services (A3 of the REST-consistency work).
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "urn:ambiquality:ingestion:value-out-of-range",
            problem.RootElement.GetProperty("type").GetString());
    }
}
