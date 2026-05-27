using HoneyDrunk.Notify.Abstractions;
using System.Diagnostics.CodeAnalysis;

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
    [SuppressMessage(
        "Security",
        "cs/exposure-of-sensitive-information",
        Justification = "NotificationChannel is a public routing enum (Email/Sms/etc.) — its name is part of the public contract, not sensitive credential material. Operator diagnostics need the human-readable channel value to triage a missing-sender misconfiguration. CodeQL pattern-matches on the literal `Email` constant name; the corresponding alert is dismissed in GHCS with this justification.")]
    public Task<DeliveryOutcome> SendAsync(NotificationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        logger.LogWarning(
            "No real sender configured. Notification {NotificationId} via {Channel} was not delivered.",
            envelope.NotificationId,
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
