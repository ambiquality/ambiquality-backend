using System.Collections.Concurrent;
using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Api.Application.Abstractions;

namespace Ambiquality.Ingestion.Api.Tests.Infrastructure;

/// <summary>
/// Test double for the durable queue: records every enqueued message in memory.
/// Set <see cref="Fail"/> to simulate an unreachable queue (drives the 503 path).
/// </summary>
public sealed class CapturingQueuePublisher : IMeasurementQueuePublisher
{
    private readonly ConcurrentQueue<MeasurementMessage> _published = new();

    public bool Fail { get; set; }

    public IReadOnlyCollection<MeasurementMessage> Published => _published;

    public Task PublishAsync(IReadOnlyList<MeasurementMessage> messages, CancellationToken cancellationToken)
    {
        if (Fail)
            throw new InvalidOperationException("Simulated queue outage.");
        foreach (var message in messages)
            _published.Enqueue(message);
        return Task.CompletedTask;
    }
}
