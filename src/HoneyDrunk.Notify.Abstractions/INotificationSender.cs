namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// The worker-side interface responsible for delivering a materialized notification envelope.
/// </summary>
/// <remarks>
/// Implementations select the appropriate provider for the envelope's channel and attempt delivery.
/// Each call represents a single delivery attempt; retry orchestration is handled by the caller.
/// </remarks>
public interface INotificationSender
{
    /// <summary>
    /// Attempts to deliver a notification envelope through the appropriate channel provider.
    /// </summary>
    /// <param name="envelope">The fully materialized notification envelope.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="DeliveryOutcome"/> describing the result of the attempt.</returns>
    Task<DeliveryOutcome> SendAsync(NotificationEnvelope envelope, CancellationToken cancellationToken = default);
}
