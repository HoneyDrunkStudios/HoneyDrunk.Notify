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
public sealed partial class NotificationDispatcher(
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

        if (runtimeOptions.MaxAttempts < 1)
        {
            throw new InvalidOperationException(
                $"NotifyRuntimeOptions.MaxAttempts must be >= 1 (configured: {runtimeOptions.MaxAttempts}). At least one attempt is required to dispatch a notification.");
        }

        DeliveryOutcome? lastOutcome = null;

        for (var attempt = 0; attempt < runtimeOptions.MaxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                var delay = backoffStrategy.Calculate(attempt - 1, runtimeOptions.BaseDelay, runtimeOptions.MaxDelay);
                LogRetrying(
                    logger,
                    envelope.NotificationId,
                    attempt + 1,
                    runtimeOptions.MaxAttempts,
                    delay.TotalMilliseconds);
                await Task.Delay(delay, timeProvider, ct);
            }

            LogDispatchAttempt(
                logger,
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
                    LogDispatchSucceeded(
                        logger,
                        NotifyEventNames.DispatchSucceeded,
                        envelope.NotificationId,
                        attempt + 1,
                        lastOutcome.Provider,
                        envelope.CorrelationId);
                    return lastOutcome;

                case DeliveryStatus.Failed when lastOutcome.FailureKind is FailureKind.Permanent or FailureKind.Policy:
                    activity?.SetTag("failure.kind", lastOutcome.FailureKind.ToString());
                    activity?.SetStatus(ActivityStatusCode.Error, lastOutcome.ErrorMessage);
                    LogDispatchFailedPermanent(
                        logger,
                        envelope.NotificationId,
                        attempt + 1,
                        lastOutcome.Provider,
                        lastOutcome.FailureKind,
                        lastOutcome.ErrorMessage,
                        envelope.CorrelationId);
                    return lastOutcome;

                case DeliveryStatus.Failed when lastOutcome.FailureKind == FailureKind.Transient:
                    activity?.SetTag("failure.kind", lastOutcome.FailureKind.ToString());
                    LogDispatchFailedTransient(
                        logger,
                        NotifyEventNames.DispatchFailed,
                        envelope.NotificationId,
                        attempt + 1,
                        lastOutcome.Provider,
                        lastOutcome.ErrorMessage,
                        envelope.CorrelationId);
                    continue;

                case DeliveryStatus.Deferred:
                    LogDispatchDeferred(
                        logger,
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
        LogAllAttemptsExhausted(
            logger,
            NotifyEventNames.DispatchFailed,
            envelope.NotificationId,
            runtimeOptions.MaxAttempts,
            envelope.CorrelationId);

        return lastOutcome!;
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Retrying {NotificationId} (attempt {Attempt}/{Max}) after {Delay}ms.")]
    private static partial void LogRetrying(
        ILogger logger,
        NotificationId notificationId,
        int attempt,
        int max,
        double delay);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "{Event}: NotificationId={NotificationId}, Attempt={Attempt}, Channel={Channel}, CorrelationId={CorrelationId}.")]
    private static partial void LogDispatchAttempt(
        ILogger logger,
        string @event,
        NotificationId notificationId,
        int attempt,
        NotificationChannel channel,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Event}: NotificationId={NotificationId}, Attempt={Attempt}, Provider={Provider}, CorrelationId={CorrelationId}.")]
    private static partial void LogDispatchSucceeded(
        ILogger logger,
        string @event,
        NotificationId notificationId,
        int attempt,
        string provider,
        string? correlationId);

    [LoggerMessage(
        EventName = NotifyEventNames.DispatchFailed,
        Level = LogLevel.Warning,
        Message = "DispatchFailed: NotificationId={NotificationId}, Attempt={Attempt}, Provider={Provider}, FailureKind={FailureKind}, Error={Error}, CorrelationId={CorrelationId}.")]
    private static partial void LogDispatchFailedPermanent(
        ILogger logger,
        NotificationId notificationId,
        int attempt,
        string provider,
        FailureKind failureKind,
        string? error,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Event}: NotificationId={NotificationId}, Attempt={Attempt}, Provider={Provider}, FailureKind=Transient, Error={Error}, CorrelationId={CorrelationId}.")]
    private static partial void LogDispatchFailedTransient(
        ILogger logger,
        string @event,
        NotificationId notificationId,
        int attempt,
        string provider,
        string? error,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Event}: NotificationId={NotificationId}, Attempt={Attempt}, Status=Deferred, CorrelationId={CorrelationId}.")]
    private static partial void LogDispatchDeferred(
        ILogger logger,
        string @event,
        NotificationId notificationId,
        int attempt,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{Event}: NotificationId={NotificationId}, MaxAttempts={Max}, CorrelationId={CorrelationId}.")]
    private static partial void LogAllAttemptsExhausted(
        ILogger logger,
        string @event,
        NotificationId notificationId,
        int max,
        string? correlationId);
}
