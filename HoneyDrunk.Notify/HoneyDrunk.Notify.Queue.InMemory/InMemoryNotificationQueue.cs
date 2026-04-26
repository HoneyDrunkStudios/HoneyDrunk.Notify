using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Queue.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace HoneyDrunk.Notify.Queue.InMemory;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="INotificationQueue"/> and <see cref="IDeadLetterInspector"/>.
/// Suitable for development and testing. Not durable across process restarts.
/// </summary>
#pragma warning disable CA1812
internal sealed class InMemoryNotificationQueue(
    IOptions<NotificationQueueOptions> options) : INotificationQueue, IDeadLetterInspector
#pragma warning restore CA1812
{
    private readonly ConcurrentQueue<InFlightItem> _available = new();
    private readonly ConcurrentDictionary<string, InFlightItem> _inFlight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DeadLetteredItem> _deadLettered = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets a snapshot of dead-lettered items for test assertions.
    /// </summary>
    internal IReadOnlyList<DeadLetteredItem> DeadLetters => [.. _deadLettered.Values];

    /// <inheritdoc />
    public Task EnqueueAsync(NotificationEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _available.Enqueue(new InFlightItem(envelope, Guid.NewGuid().ToString("N"), DeliveryCount: 0));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<QueuedNotification>> DequeueBatchAsync(int max, CancellationToken ct = default)
    {
        var effectiveMax = Math.Min(max, options.Value.MaxBatchSize);
        var results = new List<QueuedNotification>(effectiveMax);
        var now = DateTimeOffset.UtcNow;

        while (results.Count < effectiveMax && _available.TryDequeue(out var item))
        {
            var receipt = Guid.NewGuid().ToString("N");
            var deliveryCount = item.DeliveryCount + 1;
            _inFlight[receipt] = item with { DeliveryCount = deliveryCount };
            results.Add(new QueuedNotification(item.Envelope, receipt, now, deliveryCount));
        }

        return Task.FromResult<IReadOnlyList<QueuedNotification>>(results);
    }

    /// <inheritdoc />
    public Task CompleteAsync(QueuedNotification item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        _inFlight.TryRemove(item.Receipt, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AbandonAsync(QueuedNotification item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_inFlight.TryRemove(item.Receipt, out var original))
        {
            _available.Enqueue(original);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeadLetterAsync(QueuedNotification item, string reason, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (_inFlight.TryRemove(item.Receipt, out var original))
        {
            var notificationId = original.Envelope.NotificationId.ToString();
            _deadLettered[notificationId] = new DeadLetteredItem(original.Envelope, reason, DateTimeOffset.UtcNow, item.DeliveryCount);
        }

        return Task.CompletedTask;
    }

    // --- IDeadLetterInspector ---

    /// <inheritdoc />
    public Task<IReadOnlyList<DeadLetterEntry>> ListAsync(int take, CancellationToken ct = default)
    {
        var entries = _deadLettered.Values
            .OrderByDescending(d => d.DeadLetteredAt)
            .Take(take)
            .Select(ToEntry)
            .ToList();

        return Task.FromResult<IReadOnlyList<DeadLetterEntry>>(entries);
    }

    /// <inheritdoc />
    public Task<DeadLetterEntry?> FindByNotificationIdAsync(string notificationId, CancellationToken ct = default)
    {
        return Task.FromResult(
            _deadLettered.TryGetValue(notificationId, out var item) ? ToEntry(item) : null);
    }

    /// <inheritdoc />
    public Task<bool> ReplayAsync(string notificationId, CancellationToken ct = default)
    {
        if (!_deadLettered.TryRemove(notificationId, out var item))
            return Task.FromResult(false);

        _available.Enqueue(new InFlightItem(item.Envelope, Guid.NewGuid().ToString("N"), DeliveryCount: 0));
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> PurgeAsync(string notificationId, CancellationToken ct = default)
    {
        return Task.FromResult(_deadLettered.TryRemove(notificationId, out _));
    }

    private static DeadLetterEntry ToEntry(DeadLetteredItem item) =>
        new(item.NotificationId, item.DeliveryCount, item.Reason, item.Envelope)
        {
            DeadLetteredAt = item.DeadLetteredAt,
        };

    /// <summary>
    /// Represents a message that was moved to the dead-letter queue.
    /// </summary>
    /// <param name="Envelope">The original notification envelope.</param>
    /// <param name="Reason">The reason for dead-lettering (includes last failure kind).</param>
    /// <param name="DeadLetteredAt">When the message was dead-lettered.</param>
    /// <param name="DeliveryCount">How many delivery attempts were made.</param>
    internal sealed record DeadLetteredItem(
        NotificationEnvelope Envelope,
        string Reason,
        DateTimeOffset DeadLetteredAt,
        int DeliveryCount)
    {
        /// <summary>
        /// Gets the notification identifier for quick DLQ inspection.
        /// </summary>
        internal string NotificationId => Envelope.NotificationId.ToString();
    }

    private sealed record InFlightItem(NotificationEnvelope Envelope, string OriginalId, int DeliveryCount);
}
