using HoneyDrunk.Notify.Abstractions;
using System.Collections.Concurrent;

namespace HoneyDrunk.Notify.Intake;

/// <summary>
/// Thread-safe in-memory envelope queue for development and testing.
/// Envelopes can be drained via <see cref="TryDequeue"/> or <see cref="DrainAsync"/>.
/// </summary>
#pragma warning disable CA1812
internal sealed class InMemoryNotificationEnqueuer : INotificationEnqueuer
#pragma warning restore CA1812
{
    private readonly ConcurrentQueue<NotificationEnvelope> _queue = new();

    /// <inheritdoc />
    public Task EnqueueAsync(NotificationEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _queue.Enqueue(envelope);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Attempts to dequeue a single envelope. For testing and in-process consumption.
    /// </summary>
    public bool TryDequeue(out NotificationEnvelope? envelope) =>
        _queue.TryDequeue(out envelope);

    /// <summary>
    /// Drains up to <paramref name="maxItems"/> envelopes from the queue.
    /// </summary>
    public Task<IReadOnlyList<NotificationEnvelope>> DrainAsync(int maxItems, CancellationToken ct = default)
    {
        var results = new List<NotificationEnvelope>(Math.Min(maxItems, _queue.Count));

        while (results.Count < maxItems && _queue.TryDequeue(out var envelope))
        {
            results.Add(envelope);
        }

        return Task.FromResult<IReadOnlyList<NotificationEnvelope>>(results);
    }
}
