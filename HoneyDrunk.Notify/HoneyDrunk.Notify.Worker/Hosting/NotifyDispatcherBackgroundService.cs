using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Routing;
using HoneyDrunk.Notify.Worker.Options;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.Worker.Hosting;

/// <summary>
/// Background service that polls <see cref="INotificationQueue"/> and dispatches
/// envelopes through the core <see cref="NotificationDispatcher"/> (which owns retry).
/// Completes successful/permanent items, abandons transient failures for redelivery,
/// and dead-letters items that exceed the maximum delivery attempt threshold.
/// Emits structured logs with per-cycle metrics and per-item correlation.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI (AddHostedService)
internal sealed partial class NotifyDispatcherBackgroundService(
    INotificationQueue queue,
    NotificationDispatcher dispatcher,
    IOptions<NotifyWorkerOptions> workerOptions,
    IOptions<NotificationQueueOptions> queueOptions,
    ILogger<NotifyDispatcherBackgroundService> logger) : BackgroundService
#pragma warning restore CA1812
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = workerOptions.Value;

        if (!options.Enabled)
        {
            logger.LogInformation("Notify dispatcher is disabled. Idling until cancellation.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            return;
        }

        LogStarted(logger, options.PollInterval, options.BatchSize);

        var maxDeliveryAttempts = queueOptions.Value.MaxDeliveryAttempts;

        while (!stoppingToken.IsCancellationRequested)
        {
            var completedCount = 0;
            var abandonedCount = 0;
            var deadLetteredCount = 0;
            var dequeuedCount = 0;

            try
            {
                var batch = await queue.DequeueBatchAsync(options.BatchSize, stoppingToken);
                dequeuedCount = batch.Count;

                foreach (var item in batch)
                {
                    LogProcessing(
                        logger,
                        item.Envelope.NotificationId,
                        item.DeliveryCount,
                        item.Envelope.CorrelationId);

                    var outcome = await dispatcher.DispatchAsync(item.Envelope, stoppingToken);

                    if (ShouldAbandon(outcome))
                    {
                        if (item.DeliveryCount >= maxDeliveryAttempts)
                        {
                            var dlqReason = $"Max delivery attempts ({maxDeliveryAttempts}) exceeded. LastFailureKind={outcome.FailureKind}";
                            await queue.DeadLetterAsync(item, dlqReason, stoppingToken);
                            deadLetteredCount++;

                            LogDeadLettered(
                                logger,
                                NotifyEventNames.QueueDeadLettered,
                                outcome.NotificationId,
                                item.DeliveryCount,
                                outcome.Status,
                                outcome.FailureKind,
                                item.Envelope.CorrelationId);
                        }
                        else
                        {
                            await queue.AbandonAsync(item, stoppingToken);
                            abandonedCount++;

                            LogAbandoned(
                                logger,
                                outcome.NotificationId,
                                item.DeliveryCount,
                                maxDeliveryAttempts,
                                outcome.Status,
                                outcome.FailureKind,
                                item.Envelope.CorrelationId);
                        }
                    }
                    else
                    {
                        await queue.CompleteAsync(item, stoppingToken);
                        completedCount++;

                        LogCompleted(
                            logger,
                            outcome.NotificationId,
                            outcome.Channel,
                            outcome.Provider,
                            outcome.Status,
                            item.Envelope.CorrelationId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Catch broad exception to keep the polling loop alive
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Unhandled error during notification dispatch cycle.");
            }

            LogPollCycleComplete(
                logger,
                options.BatchSize,
                dequeuedCount,
                completedCount,
                abandonedCount,
                deadLetteredCount);

            await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        logger.LogInformation("Notify dispatcher stopped.");
    }

    /// <summary>
    /// Transient failures and deferred outcomes should be abandoned (redelivered).
    /// Successes, permanent failures, and policy rejections are completed (removed from queue).
    /// </summary>
    private static bool ShouldAbandon(DeliveryOutcome outcome) =>
        outcome.Status switch
        {
            DeliveryStatus.Succeeded => false,
            DeliveryStatus.Deferred => true,
            DeliveryStatus.Failed when outcome.FailureKind == FailureKind.Transient => true,
            _ => false,
        };

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Notify dispatcher started. PollInterval={PollInterval}, BatchSize={BatchSize}.")]
    private static partial void LogStarted(
        ILogger logger,
        TimeSpan pollInterval,
        int batchSize);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Processing NotificationId={NotificationId}, DeliveryCount={DeliveryCount}, CorrelationId={CorrelationId}.")]
    private static partial void LogProcessing(
        ILogger logger,
        NotificationId notificationId,
        int deliveryCount,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Event}: NotificationId={NotificationId}, DeliveryCount={DeliveryCount}, Status={Status}, FailureKind={FailureKind}, CorrelationId={CorrelationId}.")]
    private static partial void LogDeadLettered(
        ILogger logger,
        string @event,
        NotificationId notificationId,
        int deliveryCount,
        DeliveryStatus status,
        FailureKind failureKind,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Notification {NotificationId} abandoned for redelivery (attempt {DeliveryCount}/{MaxAttempts}, {Status}/{FailureKind}), CorrelationId={CorrelationId}.")]
    private static partial void LogAbandoned(
        ILogger logger,
        NotificationId notificationId,
        int deliveryCount,
        int maxAttempts,
        DeliveryStatus status,
        FailureKind failureKind,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Notification {NotificationId} completed via {Channel}/{Provider}: {Status}, CorrelationId={CorrelationId}.")]
    private static partial void LogCompleted(
        ILogger logger,
        NotificationId notificationId,
        NotificationChannel channel,
        string provider,
        DeliveryStatus status,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Poll cycle complete. BatchSize={BatchSize}, Dequeued={DequeuedCount}, Completed={CompletedCount}, Abandoned={AbandonedCount}, DeadLettered={DeadLetteredCount}.")]
    private static partial void LogPollCycleComplete(
        ILogger logger,
        int batchSize,
        int dequeuedCount,
        int completedCount,
        int abandonedCount,
        int deadLetteredCount);
}
