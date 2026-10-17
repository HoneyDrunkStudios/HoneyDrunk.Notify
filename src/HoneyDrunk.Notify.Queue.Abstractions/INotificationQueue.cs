using HoneyDrunk.Notify.Abstractions;

namespace HoneyDrunk.Notify.Queue.Abstractions;

/// <summary>
/// Vendor-neutral contract for a durable notification work queue.
/// Adapters implement this against specific queue backends (in-memory, Azure Storage Queues, etc.).
/// </summary>
#pragma warning disable CA1711 // "Queue" suffix is intentional — this IS a queue abstraction
public interface INotificationQueue
#pragma warning restore CA1711
{
    /// <summary>
    /// Enqueues a notification envelope for later dispatch.
    /// </summary>
    /// <param name="envelope">The envelope to enqueue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task EnqueueAsync(NotificationEnvelope envelope, CancellationToken ct = default);

    /// <summary>
    /// Dequeues up to <paramref name="max"/> items for processing.
    /// Dequeued items become invisible for a provider-defined visibility timeout.
    /// </summary>
    /// <param name="max">Maximum number of items to dequeue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A batch of queued notifications ready for dispatch.</returns>
    Task<IReadOnlyList<QueuedNotification>> DequeueBatchAsync(int max, CancellationToken ct = default);

    /// <summary>
    /// Acknowledges successful processing by permanently removing the item from the queue.
    /// </summary>
    /// <param name="item">The queued notification to complete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task CompleteAsync(QueuedNotification item, CancellationToken ct = default);

    /// <summary>
    /// Releases the item back to the queue for redelivery (e.g., on transient failure).
    /// </summary>
    /// <param name="item">The queued notification to abandon.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task AbandonAsync(QueuedNotification item, CancellationToken ct = default);

    /// <summary>
    /// Moves a poison message to the dead-letter queue for manual inspection.
    /// The original message is removed from the main queue.
    /// </summary>
    /// <param name="item">The queued notification to dead-letter.</param>
    /// <param name="reason">A human-readable reason for dead-lettering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeadLetterAsync(QueuedNotification item, string reason, CancellationToken ct = default);
}
