using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Diagnostics;
using HoneyDrunk.Notify.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace HoneyDrunk.Notify.Routing;

/// <summary>
/// Core dispatcher that sends a notification envelope via channel-resolved <see cref="INotificationSender"/>
/// with retry logic based on failure classification.
/// </summary>
/// <remarks>
/// Retry is owned by core runtime, not the worker. The worker simply calls
/// <see cref="DispatchAsync"/> and receives the final outcome.
/// </remarks>
public sealed class NotificationDispatcher(
    INotificationSenderResolver senderResolver,
    IOptions<NotifyRuntimeOptions> options,
    IBackoffStrategy backoffStrategy,
    TimeProvider timeProvider,
    ILogger<NotificationDispatcher> logger)
{
    /// <summary>
    /// Dispatches the envelope, retrying on transient failures up to <see cref="NotifyRuntimeOptions.MaxAttempts"/>.
    /// </summary>
    /// <param name="envelope">The notification envelope to deliver.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The final delivery outcome after all attempts are exhausted or a terminal result is reached.</returns>
    public async Task<DeliveryOutcome> DispatchAsync(NotificationEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var activity = NotifyActivitySource.Source.StartActivity(
            NotifyEventNames.DispatchAttempt, ActivityKind.Internal);

        activity?.SetTag("notification.id", envelope.NotificationId.ToString());
        activity?.SetTag("channel", envelope.Channel.ToString());
        activity?.SetTag("template.key", envelope.TemplateKey.Value);
        activity?.SetTag("correlation.id", envelope.CorrelationId);
        activity?.SetTag("tenant.id", envelope.TenantId);

        var runtimeOptions = options.Value;
        DeliveryOutcome? lastOutcome = null;

        for (var attempt = 0; attempt < runtimeOptions.MaxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                var delay = backoffStrategy.Calculate(attempt - 1, runtimeOptions.BaseDelay, runtimeOptions.MaxDelay);
                logger.LogDebug(
                    "Retrying {NotificationId} (attempt {Attempt}/{Max}) after {Delay}ms.",
                    envelope.NotificationId,
                    attempt + 1,
                    runtimeOptions.MaxAttempts,
                    delay.TotalMilliseconds);
                await Task.Delay(delay, timeProvider, ct);
            }

            logger.LogDebug(
                "{Event}: NotificationId={NotificationId}, Attempt={Attempt}, Channel={Channel}, CorrelationId={CorrelationId}.",
                NotifyEventNames.DispatchAttempt,
                envelope.NotificationId,
                attempt + 1,
                envelope.Channel,
                envelope.CorrelationId);

            var attemptEnvelope = envelope with { };
            var sender = senderResolver.Resolve(envelope.Channel);
            lastOutcome = await sender.SendAsync(attemptEnvelope, ct);

            activity?.SetTag("provider", lastOutcome.Provider);
            activity?.SetTag("delivery.status", lastOutcome.Status.ToString());

            switch (lastOutcome.Status)
            {
                case DeliveryStatus.Succeeded:
                    logger.LogInformation(
                        "{Event}: NotificationId={NotificationId}, Attempt={Attempt}, Provider={Provider}, CorrelationId={CorrelationId}.",
                        NotifyEventNames.DispatchSucceeded,
                        envelope.NotificationId,
                        attempt + 1,
                        lastOutcome.Provider,
                        envelope.CorrelationId);
                    return lastOutcome;

                case DeliveryStatus.Failed when lastOutcome.FailureKind is FailureKind.Permanent or FailureKind.Policy:
                    activity?.SetTag("failure.kind", lastOutcome.FailureKind.ToString());
                    activity?.SetStatus(ActivityStatusCode.Error, lastOutcome.ErrorMessage);
                    logger.LogWarning(
                        "{Event}: NotificationId={NotificationId}, Attempt={Attempt}, Provider={Provider}, FailureKind={FailureKind}, Error={Error}, CorrelationId={CorrelationId}.",
                        NotifyEventNames.DispatchFailed,
                        envelope.NotificationId,
                        attempt + 1,
                        lastOutcome.Provider,
                        lastOutcome.FailureKind,
                        lastOutcome.ErrorMessage,
                        envelope.CorrelationId);
                    return lastOutcome;

                case DeliveryStatus.Failed when lastOutcome.FailureKind == FailureKind.Transient:
                    activity?.SetTag("failure.kind", lastOutcome.FailureKind.ToString());
                    logger.LogWarning(
                        "{Event}: NotificationId={NotificationId}, Attempt={Attempt}, Provider={Provider}, FailureKind=Transient, Error={Error}, CorrelationId={CorrelationId}.",
                        NotifyEventNames.DispatchFailed,
                        envelope.NotificationId,
                        attempt + 1,
                        lastOutcome.Provider,
                        lastOutcome.ErrorMessage,
                        envelope.CorrelationId);
                    continue;

                case DeliveryStatus.Deferred:
                    logger.LogInformation(
                        "{Event}: NotificationId={NotificationId}, Attempt={Attempt}, Status=Deferred, CorrelationId={CorrelationId}.",
                        NotifyEventNames.DispatchAttempt,
                        envelope.NotificationId,
                        attempt + 1,
                        envelope.CorrelationId);
                    continue;

                default:
                    return lastOutcome;
            }
        }

        activity?.SetStatus(ActivityStatusCode.Error, "All attempts exhausted");
        logger.LogError(
            "{Event}: NotificationId={NotificationId}, MaxAttempts={Max}, CorrelationId={CorrelationId}.",
            NotifyEventNames.DispatchFailed,
            envelope.NotificationId,
            runtimeOptions.MaxAttempts,
            envelope.CorrelationId);

        return lastOutcome!;
    }
}
