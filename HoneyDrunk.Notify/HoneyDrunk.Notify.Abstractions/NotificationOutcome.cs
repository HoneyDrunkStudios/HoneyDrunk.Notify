namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents the result of submitting a <see cref="NotificationRequest"/> to the gateway.
/// </summary>
/// <remarks>
/// A <see cref="NotificationOutcome"/> is returned synchronously from
/// <see cref="INotificationGateway.EnqueueAsync"/> and indicates whether the request was
/// accepted for asynchronous delivery or rejected at intake.
/// </remarks>
/// <param name="NotificationId">The assigned notification identifier.</param>
/// <param name="AcceptedAtUtc">The UTC timestamp when the outcome was produced.</param>
/// <param name="Status">Whether the request was accepted or rejected.</param>
public sealed record NotificationOutcome(
    NotificationId NotificationId,
    DateTimeOffset AcceptedAtUtc,
    NotificationAcceptanceStatus Status)
{
    /// <summary>
    /// Gets the rejection reason. <see cref="RejectionReason.None"/> when
    /// <see cref="Status"/> is <see cref="NotificationAcceptanceStatus.Accepted"/>.
    /// </summary>
    public RejectionReason RejectionReason { get; init; } = RejectionReason.None;

    /// <summary>
    /// Gets an optional human-readable rejection message providing additional context.
    /// </summary>
    public string? RejectionDetail { get; init; }

    /// <summary>
    /// Creates an accepted outcome.
    /// </summary>
    /// <param name="notificationId">The assigned notification identifier.</param>
    /// <param name="acceptedAtUtc">The UTC timestamp of acceptance.</param>
    /// <returns>A new accepted <see cref="NotificationOutcome"/>.</returns>
    public static NotificationOutcome Accepted(NotificationId notificationId, DateTimeOffset acceptedAtUtc) =>
        new(notificationId, acceptedAtUtc, NotificationAcceptanceStatus.Accepted);

    /// <summary>
    /// Creates a rejected outcome.
    /// </summary>
    /// <param name="notificationId">The assigned notification identifier.</param>
    /// <param name="rejectedAtUtc">The UTC timestamp of rejection.</param>
    /// <param name="reason">The reason the request was rejected.</param>
    /// <param name="detail">Optional human-readable detail.</param>
    /// <returns>A new rejected <see cref="NotificationOutcome"/>.</returns>
    public static NotificationOutcome Rejected(
        NotificationId notificationId,
        DateTimeOffset rejectedAtUtc,
        RejectionReason reason,
        string? detail = null) =>
        new(notificationId, rejectedAtUtc, NotificationAcceptanceStatus.Rejected)
        {
            RejectionReason = reason,
            RejectionDetail = detail,
        };
}
