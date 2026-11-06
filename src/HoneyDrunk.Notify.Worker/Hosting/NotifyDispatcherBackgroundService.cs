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
#pragma warning disable SA1204 // Static helpers placed adjacent to their callers for readability.
internal sealed class NotifyDispatcherBackgroundService(
    INotificationQueue queue,
    NotificationDispatcher dispatcher,
    IOptions<NotifyWorkerOptions> workerOptions,
    IOptions<NotificationQueueOptions> queueOptions,
    ILogger<NotifyDispatcherBackgroundService> logger) : BackgroundService
#pragma warning restore CA1812
{
    private enum ItemDisposition
    {
        Completed,
        Abandoned,
        DeadLettered,
    }

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

        NotifyDispatcherLog.LogStarted(logger, options.PollInterval, options.BatchSize);

        var maxDeliveryAttempts = queueOptions.Value.MaxDeliveryAttempts;

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunPollCycleAsync(options, maxDeliveryAttempts, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        logger.LogInformation("Notify dispatcher stopped.");
    }

    private async Task RunPollCycleAsync(
        NotifyWorkerOptions options,
        int maxDeliveryAttempts,
        CancellationToken stoppingToken)
    {
        var stats = default(PollCycleStats);

        try
        {
            var batch = await queue.DequeueBatchAsync(options.BatchSize, stoppingToken);
            stats.Dequeued = batch.Count;

            foreach (var item in batch)
            {
                var disposition = await ProcessItemAsync(item, maxDeliveryAttempts, stoppingToken);
                switch (disposition)
                {
                    case ItemDisposition.Completed:
                        stats.Completed++;
                        break;
                    case ItemDisposition.DeadLettered:
                        stats.DeadLettered++;
                        break;
                    case ItemDisposition.Abandoned:
                        stats.Abandoned++;
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
#pragma warning disable CA1031 // Catch broad exception to keep the polling loop alive
        // lgtm[cs/catch-of-all-exceptions] background-poller safety net: the dispatcher can throw
        // arbitrary provider exceptions and the loop must survive them; OperationCanceledException
        // is filtered above.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Unhandled error during notification dispatch cycle.");
        }

        NotifyDispatcherLog.LogPollCycleComplete(
            logger,
            options.BatchSize,
            stats.Dequeued,
            stats.Completed,
            stats.Abandoned,
            stats.DeadLettered);
    }

    private async Task<ItemDisposition> ProcessItemAsync(
        QueuedNotification item,
        int maxDeliveryAttempts,
        CancellationToken stoppingToken)
    {
        NotifyDispatcherLog.LogProcessing(
            logger,
            item.Envelope.NotificationId,
            item.DeliveryCount,
            item.Envelope.CorrelationId);

        var outcome = await dispatcher.DispatchAsync(item.Envelope, stoppingToken);

        if (!ShouldAbandon(outcome))
        {
            await queue.CompleteAsync(item, stoppingToken);
            NotifyDispatcherLog.LogCompleted(
                logger,
                outcome.NotificationId,
                outcome.Channel,
                outcome.Provider,
                outcome.Status,
                item.Envelope.CorrelationId);
            return ItemDisposition.Completed;
        }

        if (item.DeliveryCount >= maxDeliveryAttempts)
        {
            var dlqReason = $"Max delivery attempts ({maxDeliveryAttempts}) exceeded. LastFailureKind={outcome.FailureKind}";
            await queue.DeadLetterAsync(item, dlqReason, stoppingToken);
            NotifyDispatcherLog.LogDeadLettered(
                logger,
                NotifyEventNames.QueueDeadLettered,
                outcome.NotificationId,
                item.DeliveryCount,
                outcome.Status,
                outcome.FailureKind,
                item.Envelope.CorrelationId);
            return ItemDisposition.DeadLettered;
        }

        await queue.AbandonAsync(item, stoppingToken);
        NotifyDispatcherLog.LogAbandoned(
            logger,
            outcome.NotificationId,
            item.DeliveryCount,
            maxDeliveryAttempts,
            outcome.Status,
            outcome.FailureKind,
            item.Envelope.CorrelationId);
        return ItemDisposition.Abandoned;
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

    private struct PollCycleStats
    {
        public int Dequeued;
        public int Completed;
        public int Abandoned;
        public int DeadLettered;
    }
}
#pragma warning restore SA1204
