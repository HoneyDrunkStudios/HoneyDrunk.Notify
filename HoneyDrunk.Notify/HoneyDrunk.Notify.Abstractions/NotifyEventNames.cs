namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Well-known event names for structured logging and tracing across the notification pipeline.
/// </summary>
public static class NotifyEventNames
{
    /// <summary>Notification accepted and enqueued for delivery.</summary>
    public const string EnqueueAccepted = "Notify.Enqueue.Accepted";

    /// <summary>Notification rejected at intake (validation, policy, or deduplication).</summary>
    public const string EnqueueRejected = "Notify.Enqueue.Rejected";

    /// <summary>A delivery attempt has started.</summary>
    public const string DispatchAttempt = "Notify.Dispatch.Attempt";

    /// <summary>Delivery attempt succeeded.</summary>
    public const string DispatchSucceeded = "Notify.Dispatch.Succeeded";

    /// <summary>Delivery attempt failed (transient or permanent).</summary>
    public const string DispatchFailed = "Notify.Dispatch.Failed";

    /// <summary>Message moved to the dead-letter queue after exceeding max delivery attempts.</summary>
    public const string QueueDeadLettered = "Notify.Queue.DeadLettered";
}
