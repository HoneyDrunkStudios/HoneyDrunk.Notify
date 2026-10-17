namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents the acceptance status of a <see cref="NotificationRequest"/>
/// after initial validation and policy evaluation.
/// </summary>
public enum NotificationAcceptanceStatus
{
    /// <summary>
    /// The request was accepted and enqueued for delivery.
    /// </summary>
    Accepted = 0,

    /// <summary>
    /// The request was rejected during intake (validation failure, policy denial, etc.).
    /// </summary>
    Rejected = 1,
}
