using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Routing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HoneyDrunk.Notify.Functions;

/// <summary>
/// Azure Function triggered by the notify Azure Storage Queue.
/// Deserializes the <see cref="NotificationEnvelope"/> and dispatches it
/// through the core <see cref="NotificationDispatcher"/> retry engine.
/// </summary>
public sealed partial class NotifyDispatcherFunction(
    NotificationDispatcher dispatcher,
    ILogger<NotifyDispatcherFunction> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Dispatches a serialized notification envelope from Azure Storage Queue.
    /// </summary>
    /// <param name="message">The serialized notification envelope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous dispatch operation.</returns>
    [Function(nameof(NotifyDispatcherFunction))]
    public async Task Run(
        [QueueTrigger("notify-queue", Connection = "NotifyQueueConnection")] string message,
        CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<NotificationEnvelope>(message, SerializerOptions);

        if (envelope is null)
        {
            logger.LogError("Failed to deserialize notification envelope from queue message.");
            return;
        }

        LogDispatching(
            logger,
            envelope.NotificationId,
            envelope.Channel,
            envelope.CorrelationId);

        var outcome = await dispatcher.DispatchAsync(envelope, cancellationToken);

        if (outcome.Status == DeliveryStatus.Failed)
        {
            // Throwing causes the Functions runtime to retry/dead-letter based on host.json config
            throw new InvalidOperationException(
                $"Notification {outcome.NotificationId} delivery failed: {outcome.FailureKind} — {outcome.ErrorMessage}");
        }

        LogDispatchedSuccessfully(logger, outcome.NotificationId, outcome.Provider);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Dispatching NotificationId={NotificationId}, Channel={Channel}, CorrelationId={CorrelationId}.")]
    private static partial void LogDispatching(
        ILogger logger,
        NotificationId notificationId,
        NotificationChannel channel,
        string? correlationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Notification {NotificationId} dispatched successfully via {Provider}.")]
    private static partial void LogDispatchedSuccessfully(
        ILogger logger,
        NotificationId notificationId,
        string provider);
}
