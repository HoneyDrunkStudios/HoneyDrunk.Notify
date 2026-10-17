namespace HoneyDrunk.Notify.Queue.Abstractions;

/// <summary>
/// Base options for notification queue adapters.
/// </summary>
public class NotificationQueueOptions
{
    /// <summary>
    /// Gets or sets the queue name. Defaults to "notify-queue".
    /// </summary>
    public string QueueName { get; set; } = "notify-queue";

    /// <summary>
    /// Gets or sets the maximum number of messages to dequeue per batch.
    /// </summary>
    public int MaxBatchSize { get; set; } = 16;

    /// <summary>
    /// Gets or sets how long a dequeued message stays invisible before being redelivered.
    /// </summary>
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before a message is dead-lettered.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 10;

    /// <summary>
    /// Gets or sets the dead-letter queue name. Defaults to QueueName + "-dlq".
    /// When null, derived as <c>{QueueName}-dlq</c> at runtime.
    /// </summary>
    public string? DeadLetterQueueName { get; set; }

    /// <summary>
    /// Gets the effective DLQ name, falling back to <c>{QueueName}-dlq</c>.
    /// </summary>
    public string EffectiveDeadLetterQueueName => DeadLetterQueueName ?? $"{QueueName}-dlq";
}
