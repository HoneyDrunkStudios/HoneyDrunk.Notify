using Azure.Storage.Queues;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Queue.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HoneyDrunk.Notify.Queue.AzureStorage;

/// <summary>
/// Azure Storage Queue implementation of <see cref="INotificationQueue"/>.
/// Serializes envelopes as JSON, encodes receipts as "messageId|popReceipt".
/// </summary>
#pragma warning disable CA1812
internal sealed class AzureStorageNotificationQueue : INotificationQueue, IDeadLetterInspector, IAsyncDisposable
#pragma warning restore CA1812
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AzureStorageQueueOptions _options;
    private readonly ILogger<AzureStorageNotificationQueue> _logger;
    private readonly QueueClient _client;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private QueueClient? _dlqClient;
    private bool _initialized;
    private bool _dlqInitialized;
    private bool _disposed;

    public AzureStorageNotificationQueue(
        IOptions<AzureStorageQueueOptions> options,
        ILogger<AzureStorageNotificationQueue> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException("AzureStorageQueueOptions.ConnectionString is required.");

        _client = new QueueClient(_options.ConnectionString, _options.QueueName, new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64,
        });
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(NotificationEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        await EnsureQueueExistsAsync(ct);

        var json = JsonSerializer.Serialize(envelope, SerializerOptions);
        await _client.SendMessageAsync(json, ct);

        _logger.LogDebug(
            "Enqueued notification {NotificationId} to Azure Storage Queue '{Queue}'.",
            envelope.NotificationId,
            _options.QueueName);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedNotification>> DequeueBatchAsync(int max, CancellationToken ct = default)
    {
        await EnsureQueueExistsAsync(ct);

        var effectiveMax = Math.Min(max, _options.MaxBatchSize);
        var response = await _client.ReceiveMessagesAsync(effectiveMax, _options.VisibilityTimeout, ct);

        if (response.Value is null || response.Value.Length == 0)
            return [];

        var results = new List<QueuedNotification>(response.Value.Length);
        var now = DateTimeOffset.UtcNow;

        foreach (var msg in response.Value)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<NotificationEnvelope>(msg.MessageText, SerializerOptions);

                if (envelope is null)
                {
                    _logger.LogWarning("Failed to deserialize message {MessageId}. Skipping.", msg.MessageId);
                    continue;
                }

                var receipt = EncodeReceipt(msg.MessageId, msg.PopReceipt);
                results.Add(new QueuedNotification(envelope, receipt, now, (int)msg.DequeueCount));
            }
#pragma warning disable CA1031
            catch (JsonException ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "Deserialization failed for message {MessageId}. Deleting poison message.", msg.MessageId);
                await _client.DeleteMessageAsync(msg.MessageId, msg.PopReceipt, ct);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(QueuedNotification item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var (messageId, popReceipt) = DecodeReceipt(item.Receipt);
        await _client.DeleteMessageAsync(messageId, popReceipt, ct);
    }

    /// <inheritdoc />
    public async Task AbandonAsync(QueuedNotification item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var (messageId, popReceipt) = DecodeReceipt(item.Receipt);

        // Near-zero visibility timeout makes the message immediately available for redelivery
        await _client.UpdateMessageAsync(messageId, popReceipt, visibilityTimeout: TimeSpan.FromSeconds(1), cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task DeadLetterAsync(QueuedNotification item, string reason, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await EnsureDlqExistsAsync(ct);

        var wrapper = new DeadLetterWrapper(item.Envelope, reason, DateTimeOffset.UtcNow, item.DeliveryCount)
        {
            NotificationId = item.Envelope.NotificationId.ToString(),
        };
        var json = JsonSerializer.Serialize(wrapper, SerializerOptions);
        await _dlqClient!.SendMessageAsync(json, ct);

        var (messageId, popReceipt) = DecodeReceipt(item.Receipt);
        await _client.DeleteMessageAsync(messageId, popReceipt, ct);

        _logger.LogWarning(
            "Dead-lettered notification {NotificationId} after {DeliveryCount} attempts. Reason: {Reason}.",
            item.Envelope.NotificationId,
            item.DeliveryCount,
            reason);
    }

    // --- IDeadLetterInspector ---

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeadLetterEntry>> ListAsync(int take, CancellationToken ct = default)
    {
        await EnsureDlqExistsAsync(ct);

        // Azure Storage Queues PeekMessages supports max 32 per call
        var effectiveTake = Math.Min(take, 32);
        var response = await _dlqClient!.PeekMessagesAsync(effectiveTake, ct);

        if (response.Value is null || response.Value.Length == 0)
            return [];

        var results = new List<DeadLetterEntry>(response.Value.Length);

        foreach (var msg in response.Value)
        {
            var wrapper = TryDeserializeWrapper(msg.MessageText);
            if (wrapper is not null)
                results.Add(WrapperToEntry(wrapper));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<DeadLetterEntry?> FindByNotificationIdAsync(string notificationId, CancellationToken ct = default)
    {
        await EnsureDlqExistsAsync(ct);

        var response = await _dlqClient!.PeekMessagesAsync(32, ct);

        if (response.Value is null)
            return null;

        foreach (var msg in response.Value)
        {
            var wrapper = TryDeserializeWrapper(msg.MessageText);
            if (wrapper?.NotificationId == notificationId)
                return WrapperToEntry(wrapper);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<bool> ReplayAsync(string notificationId, CancellationToken ct = default)
    {
        return await MutateDlqItemAsync(notificationId, replay: true, ct);
    }

    /// <inheritdoc />
    public async Task<bool> PurgeAsync(string notificationId, CancellationToken ct = default)
    {
        return await MutateDlqItemAsync(notificationId, replay: false, ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _initLock.Dispose();
        await Task.CompletedTask;
    }

    private static DeadLetterEntry WrapperToEntry(DeadLetterWrapper wrapper) =>
        new(wrapper.NotificationId ?? string.Empty, wrapper.DeliveryCount, wrapper.Reason, wrapper.Envelope)
        {
            DeadLetteredAt = wrapper.DeadLetteredAt,
        };

    // Receipt format: "{messageId}|{popReceipt}"
    private static string EncodeReceipt(string messageId, string popReceipt) =>
        $"{messageId}|{popReceipt}";

    private static (string messageId, string popReceipt) DecodeReceipt(string receipt)
    {
        var separatorIndex = receipt.IndexOf('|', StringComparison.Ordinal);

        if (separatorIndex < 0)
            throw new FormatException($"Invalid queue receipt format: '{receipt}'.");

        return (receipt[..separatorIndex], receipt[(separatorIndex + 1)..]);
    }

    /// <summary>
    /// Receives DLQ messages in batches, finds the target notification, and optionally replays it.
    /// Non-matching messages are re-enqueued into the DLQ in their original form.
    /// </summary>
    private async Task<bool> MutateDlqItemAsync(string notificationId, bool replay, CancellationToken ct)
    {
        await EnsureDlqExistsAsync(ct);
        await EnsureQueueExistsAsync(ct);

        // Azure Storage Queues don't support selective delete; receive a batch and scan
        var response = await _dlqClient!.ReceiveMessagesAsync(32, TimeSpan.FromSeconds(30), ct);

        if (response.Value is null || response.Value.Length == 0)
            return false;

        var found = false;

        foreach (var msg in response.Value)
        {
            var wrapper = TryDeserializeWrapper(msg.MessageText);
            if (!found && wrapper?.NotificationId == notificationId)
            {
                found = true;

                if (replay && wrapper.Envelope is not null)
                {
                    var json = JsonSerializer.Serialize(wrapper.Envelope, SerializerOptions);
                    await _client.SendMessageAsync(json, ct);
                }

                await _dlqClient.DeleteMessageAsync(msg.MessageId, msg.PopReceipt, ct);
            }
            else
            {
                // Return non-matching messages to DLQ immediately
                await _dlqClient.UpdateMessageAsync(
                    msg.MessageId,
                    msg.PopReceipt,
                    visibilityTimeout: TimeSpan.FromSeconds(1),
                    cancellationToken: ct);
            }
        }

        return found;
    }

    private DeadLetterWrapper? TryDeserializeWrapper(string messageText)
    {
        try
        {
            return JsonSerializer.Deserialize<DeadLetterWrapper>(messageText, SerializerOptions);
        }
#pragma warning disable CA1031
        catch (JsonException)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    private async Task EnsureQueueExistsAsync(CancellationToken ct)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized)
            {
                return;
            }

            if (_options.CreateIfNotExists)
                await _client.CreateIfNotExistsAsync(cancellationToken: ct);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task EnsureDlqExistsAsync(CancellationToken ct)
    {
        if (_dlqInitialized)
        {
            return;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_dlqInitialized)
            {
                return;
            }

            _dlqClient = new QueueClient(_options.ConnectionString, _options.EffectiveDeadLetterQueueName, new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64,
            });

            if (_options.CreateIfNotExists)
                await _dlqClient.CreateIfNotExistsAsync(cancellationToken: ct);

            _dlqInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private sealed record DeadLetterWrapper(
        NotificationEnvelope Envelope,
        string Reason,
        DateTimeOffset DeadLetteredAt,
        int DeliveryCount)
    {
        /// <summary>
        /// Gets the notification identifier for quick DLQ inspection without deserializing the full envelope.
        /// </summary>
        public string? NotificationId { get; init; } = Envelope?.NotificationId.ToString();
    }
}
