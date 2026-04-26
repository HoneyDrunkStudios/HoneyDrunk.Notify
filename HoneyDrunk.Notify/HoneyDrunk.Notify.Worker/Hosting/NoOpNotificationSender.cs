using HoneyDrunk.Notify.Abstractions;

namespace HoneyDrunk.Notify.Worker.Hosting;

/// <summary>
/// Placeholder sender that logs envelopes without delivering them.
/// Replaced by a real sender once provider implementations are registered.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class NoOpNotificationSender(ILogger<NoOpNotificationSender> logger) : INotificationSender
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public Task<DeliveryOutcome> SendAsync(NotificationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        logger.LogWarning(
            "No real sender configured. Notification {NotificationId} to {Address} via {Channel} was not delivered.",
            envelope.NotificationId,
            envelope.Recipient.Address,
            envelope.Channel);

        var outcome = DeliveryOutcome.Failed(
            envelope.NotificationId,
            AttemptId.NewId(),
            envelope.Channel,
            "noop",
            FailureKind.Permanent,
            "No sender implementation is registered.");

        return Task.FromResult(outcome);
    }
}
