using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Worker;
using Npgsql;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

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

var host = builder.Build();
host.Run();
