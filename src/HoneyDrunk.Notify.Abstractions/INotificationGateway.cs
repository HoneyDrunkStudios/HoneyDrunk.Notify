namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// The caller-facing entry point for submitting notifications.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="INotificationGateway"/> is the primary interface that application code uses to
/// request a notification. Implementations validate the request, assign a
/// <see cref="NotificationId"/>, and enqueue a <see cref="NotificationEnvelope"/> for
/// asynchronous delivery by the worker.
/// </para>
/// <para>
/// The returned <see cref="NotificationOutcome"/> tells the caller whether the request was
/// accepted or rejected — it does <em>not</em> indicate delivery success, which is inherently
/// asynchronous.
/// </para>
/// </remarks>
public interface INotificationGateway
{
    /// <summary>
    /// Validates and enqueues a notification for asynchronous delivery.
    /// </summary>
    /// <param name="request">The notification request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="NotificationOutcome"/> indicating whether the request was accepted or rejected.
    /// </returns>
    Task<NotificationOutcome> EnqueueAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
