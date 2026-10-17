namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents the priority level of a notification, influencing queue ordering and delivery urgency.
/// </summary>
public enum NotificationPriority
{
    /// <summary>
    /// Standard priority. Processed in FIFO order with no expedited handling.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Elevated priority. Moved ahead of normal-priority items in the processing queue.
    /// </summary>
    High = 1,
}
