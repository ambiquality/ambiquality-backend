using System.Net;
using System.Net.Http.Json;
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

    private async Task<HttpResponseMessage> PostAsync(
        Guid sensorId, string parameterCode, double value, string? apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/measurements")
        {
            Content = JsonContent.Create(new
            {
                sensorId,
                parameterCode,
                value,
                observedAt = DateTime.UtcNow,
            }),
        };
        if (apiKey is not null)
            request.Headers.Add(MeasurementEndpoints.SensorKeyHeader, apiKey);

        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task ValidObservation_Returns201AndPersists()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2", "temperature"]);

        var response = await PostAsync(sensorId, "co2", 812, apiKey);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeasurementAcceptedResponse>();
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal(1, await _factory.CountMeasurementsAsync(sensorId));
    }

    [Fact]
    public async Task MissingKey_Returns401()
    {
        var (sensorId, _) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostAsync(sensorId, "co2", 800, apiKey: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await _factory.CountMeasurementsAsync(sensorId));
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
    public async Task InactiveSensor_Returns403()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"], statusCode: "maintenance");

        var response = await PostAsync(sensorId, "co2", 800, apiKey);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await _factory.CountMeasurementsAsync(sensorId));
    }

    [Fact]
    public async Task UndeclaredParameter_Returns422()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostAsync(sensorId, "humidity", 50, apiKey);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await _factory.CountMeasurementsAsync(sensorId));
    }

    [Fact]
    public async Task ValueOutOfRange_Returns422()
    {
        var (sensorId, apiKey) = await _factory.SeedSensorAsync(["co2"]);

        var response = await PostAsync(sensorId, "co2", 999_999, apiKey);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await _factory.CountMeasurementsAsync(sensorId));
    }
}
