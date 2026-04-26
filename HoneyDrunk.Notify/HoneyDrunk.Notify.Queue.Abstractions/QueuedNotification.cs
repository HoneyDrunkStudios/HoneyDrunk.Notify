using HoneyDrunk.Notify.Abstractions;

namespace HoneyDrunk.Notify.Queue.Abstractions;

/// <summary>
/// Represents a notification envelope that has been dequeued for processing.
/// The <see cref="Receipt"/> is an opaque token used by the queue adapter to complete or abandon the item.
/// </summary>
/// <param name="Envelope">The notification envelope to be dispatched.</param>
/// <param name="Receipt">Opaque receipt token for ack/nack operations. Format is adapter-specific.</param>
/// <param name="DequeuedAt">Timestamp when the item was dequeued.</param>
/// <param name="DeliveryCount">How many times this message has been dequeued (including this attempt).</param>
public sealed record QueuedNotification(
    NotificationEnvelope Envelope,
    string Receipt,
    DateTimeOffset DequeuedAt,
    int DeliveryCount = 1);
