using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Worker;
using Ambiquality.Ingestion.Worker.Monitoring;
using Ambiquality.Observability;
using Npgsql;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

// --- Observability -----------------------------------------------------------
// Runtime + queue-saturation metrics (queue length, unacked backlog, drain gap)
// exposed on the worker's internal metrics port (Observability:MetricsPort).
var observabilityEnabled = ObservabilityExtensions.IsEnabled(builder.Configuration);
var observabilityMetricsPort = ObservabilityExtensions.ResolveMetricsPort(builder.Configuration, 9468);
if (observabilityEnabled)
    builder.Services.AddAmbiqualityWorkerMetrics(observabilityMetricsPort);

builder.Services.Configure<MeasurementQueueOptions>(
    builder.Configuration.GetSection(MeasurementQueueOptions.SectionName));

// ieq: read-write. The worker is the only writer to the measurements hypertable.
builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("IeqDb")
        ?? throw new InvalidOperationException("Missing 'IeqDb' connection string.");
    return NpgsqlDataSource.Create(connectionString);
});

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Missing 'Redis' connection string.");
    return ConnectionMultiplexer.Connect(connectionString);
});

builder.Services.AddSingleton<MeasurementBatchWriter>();
builder.Services.AddHostedService<MeasurementDrainService>();

// Queue saturation gauges (stream length / unacked backlog / drain gap).
builder.Services.AddSingleton<DrainStatus>();
builder.Services.AddHostedService<QueueMetricsService>();

var host = builder.Build();
host.Run();
