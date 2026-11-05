using HoneyDrunk.Notify.Abstractions;

namespace HoneyDrunk.Notify.Worker.Hosting;

/// <summary>
/// Structured log events for the notification queue polling and dispatch lifecycle.
/// </summary>
internal static partial class NotifyDispatcherLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Notify dispatcher started. PollInterval={PollInterval}, BatchSize={BatchSize}.")]
    internal static partial void LogStarted(
        ILogger logger,
        TimeSpan pollInterval,
        int batchSize);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Processing NotificationId={NotificationId}, DeliveryCount={DeliveryCount}, CorrelationId={CorrelationId}.")]
    internal static partial void LogProcessing(
        ILogger logger,
        NotificationId notificationId,
        int deliveryCount,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Event}: NotificationId={NotificationId}, DeliveryCount={DeliveryCount}, Status={Status}, FailureKind={FailureKind}, CorrelationId={CorrelationId}.")]
    internal static partial void LogDeadLettered(
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
    internal static partial void LogAbandoned(
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
    internal static partial void LogCompleted(
        ILogger logger,
        NotificationId notificationId,
        NotificationChannel channel,
        string provider,
        DeliveryStatus status,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Poll cycle complete. BatchSize={BatchSize}, Dequeued={DequeuedCount}, Completed={CompletedCount}, Abandoned={AbandonedCount}, DeadLettered={DeadLetteredCount}.")]
    internal static partial void LogPollCycleComplete(
        ILogger logger,
        int batchSize,
        int dequeuedCount,
        int completedCount,
        int abandonedCount,
        int deadLetteredCount);
}
