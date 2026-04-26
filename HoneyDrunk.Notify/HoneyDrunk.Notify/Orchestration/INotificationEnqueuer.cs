using HoneyDrunk.Notify.Abstractions;

namespace HoneyDrunk.Notify.Orchestration;

/// <summary>
/// Accepts materialized notification envelopes for asynchronous delivery.
/// </summary>
/// <remarks>
/// The gateway produces envelopes and hands them to the enqueuer.
/// The worker-side dequeues them via its own work source abstraction.
/// </remarks>
public interface INotificationEnqueuer
{
    /// <summary>
    /// Enqueues a notification envelope for later dispatch.
    /// </summary>
    /// <param name="envelope">The fully-built envelope to deliver.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task EnqueueAsync(NotificationEnvelope envelope, CancellationToken ct = default);
}
