namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents the result of a single delivery attempt by the notification sender.
/// </summary>
/// <remarks>
/// <para>
/// Returned by <see cref="INotificationSender.SendAsync"/> to report whether a provider
/// successfully delivered the notification, failed, or deferred it for later processing.
/// </para>
/// <para>
/// When <see cref="Status"/> is <see cref="DeliveryStatus.Failed"/>, the
/// <see cref="FailureKind"/> property indicates whether the failure is retryable.
/// </para>
/// </remarks>
/// <param name="NotificationId">The notification this attempt belongs to.</param>
/// <param name="AttemptId">The unique identifier for this delivery attempt.</param>
/// <param name="Channel">The channel used for delivery.</param>
/// <param name="Provider">The name of the provider that handled the attempt (e.g., "smtp", "sendgrid").</param>
/// <param name="Status">The outcome of the delivery attempt.</param>
public sealed record DeliveryOutcome(
    NotificationId NotificationId,
    AttemptId AttemptId,
    NotificationChannel Channel,
    string Provider,
    DeliveryStatus Status)
{
    /// <summary>
    /// Gets the failure classification. <see cref="Abstractions.FailureKind.None"/> when delivery succeeded.
    /// </summary>
    public FailureKind FailureKind { get; init; } = FailureKind.None;

    /// <summary>
    /// Gets the provider-assigned message identifier, when available.
    /// </summary>
    public string? ProviderMessageId { get; init; }

    /// <summary>
    /// Gets an optional human-readable error message when the attempt failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful delivery outcome.
    /// </summary>
    /// <param name="notificationId">The notification identifier.</param>
    /// <param name="attemptId">The attempt identifier.</param>
    /// <param name="channel">The delivery channel.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="providerMessageId">Optional provider-assigned message identifier.</param>
    /// <returns>A new successful <see cref="DeliveryOutcome"/>.</returns>
    public static DeliveryOutcome Succeeded(
        NotificationId notificationId,
        AttemptId attemptId,
        NotificationChannel channel,
        string provider,
        string? providerMessageId = null) =>
        new(notificationId, attemptId, channel, provider, DeliveryStatus.Succeeded)
        {
            ProviderMessageId = providerMessageId,
        };

    /// <summary>
    /// Creates a failed delivery outcome.
    /// </summary>
    /// <param name="notificationId">The notification identifier.</param>
    /// <param name="attemptId">The attempt identifier.</param>
    /// <param name="channel">The delivery channel.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="failureKind">The failure classification.</param>
    /// <param name="errorMessage">Optional error detail.</param>
    /// <returns>A new failed <see cref="DeliveryOutcome"/>.</returns>
    public static DeliveryOutcome Failed(
        NotificationId notificationId,
        AttemptId attemptId,
        NotificationChannel channel,
        string provider,
        FailureKind failureKind,
        string? errorMessage = null) =>
        new(notificationId, attemptId, channel, provider, DeliveryStatus.Failed)
        {
            FailureKind = failureKind,
            ErrorMessage = errorMessage,
        };

    /// <summary>
    /// Creates a deferred delivery outcome.
    /// </summary>
    /// <param name="notificationId">The notification identifier.</param>
    /// <param name="attemptId">The attempt identifier.</param>
    /// <param name="channel">The delivery channel.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="providerMessageId">Optional provider-assigned message identifier.</param>
    /// <returns>A new deferred <see cref="DeliveryOutcome"/>.</returns>
    public static DeliveryOutcome Deferred(
        NotificationId notificationId,
        AttemptId attemptId,
        NotificationChannel channel,
        string provider,
        string? providerMessageId = null) =>
        new(notificationId, attemptId, channel, provider, DeliveryStatus.Deferred)
        {
            ProviderMessageId = providerMessageId,
        };
}
