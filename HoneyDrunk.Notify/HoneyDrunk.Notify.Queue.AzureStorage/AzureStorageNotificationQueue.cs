using Azure.Storage.Queues;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HoneyDrunk.Notify.Queue.AzureStorage;

/// <summary>
/// Azure Storage Queue implementation of <see cref="INotificationQueue"/>.
/// Serializes envelopes as JSON, encodes receipts as "messageId|popReceipt".
/// </summary>
#pragma warning disable CA1812
internal sealed partial class AzureStorageNotificationQueue : INotificationQueue, IDeadLetterInspector, IAsyncDisposable
#pragma warning restore CA1812
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AzureStorageQueueOptions _options;
    private readonly ILogger<AzureStorageNotificationQueue> _logger;
    private readonly ISecretStore? _secretStore;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private QueueClient? _client;
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
    }

    public AzureStorageNotificationQueue(
        IOptions<AzureStorageQueueOptions> options,
        ILogger<AzureStorageNotificationQueue> logger,
        ISecretStore secretStore)
    {
        _options = options.Value;
        _logger = logger;
        _secretStore = secretStore;
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(NotificationEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var client = await EnsureQueueExistsAsync(ct);

        var json = JsonSerializer.Serialize(envelope, SerializerOptions);
        await client.SendMessageAsync(json, ct);

        LogEnqueued(_logger, envelope.NotificationId, _options.QueueName);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedNotification>> DequeueBatchAsync(int max, CancellationToken ct = default)
    {
        var client = await EnsureQueueExistsAsync(ct);

        var effectiveMax = Math.Min(max, _options.MaxBatchSize);
        var response = await client.ReceiveMessagesAsync(effectiveMax, _options.VisibilityTimeout, ct);

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
                    LogDeserializeNull(_logger, msg.MessageId);
                    continue;
                }

                var receipt = EncodeReceipt(msg.MessageId, msg.PopReceipt);
                results.Add(new QueuedNotification(envelope, receipt, now, (int)msg.DequeueCount));
            }
#pragma warning disable CA1031
            catch (JsonException ex)
#pragma warning restore CA1031
            {
                LogPoisonMessage(_logger, ex, msg.MessageId);
                await client.DeleteMessageAsync(msg.MessageId, msg.PopReceipt, ct);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(QueuedNotification item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var client = await EnsureQueueExistsAsync(ct);
        var (messageId, popReceipt) = DecodeReceipt(item.Receipt);
        await client.DeleteMessageAsync(messageId, popReceipt, ct);
    }

    /// <inheritdoc />
    public async Task AbandonAsync(QueuedNotification item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var client = await EnsureQueueExistsAsync(ct);
        var (messageId, popReceipt) = DecodeReceipt(item.Receipt);

        // Near-zero visibility timeout makes the message immediately available for redelivery
        await client.UpdateMessageAsync(messageId, popReceipt, visibilityTimeout: TimeSpan.FromSeconds(1), cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task DeadLetterAsync(QueuedNotification item, string reason, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var client = await EnsureQueueExistsAsync(ct);
        await EnsureDlqExistsAsync(ct);

        var wrapper = new DeadLetterWrapper(item.Envelope, reason, DateTimeOffset.UtcNow, item.DeliveryCount)
        {
            NotificationId = item.Envelope.NotificationId.ToString(),
        };
        var json = JsonSerializer.Serialize(wrapper, SerializerOptions);
        await _dlqClient!.SendMessageAsync(json, ct);

        var (messageId, popReceipt) = DecodeReceipt(item.Receipt);
        await client.DeleteMessageAsync(messageId, popReceipt, ct);

        LogDeadLettered(_logger, item.Envelope.NotificationId, item.DeliveryCount, reason);
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

    internal async Task<string> ResolveConnectionStringAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            return _options.ConnectionString;
        }

        if (_secretStore is null)
        {
            throw new InvalidOperationException(
                "Azure Storage Queue connection string must be provided directly for local tooling or resolved through ISecretStore for hosted workloads.");
        }

        if (string.IsNullOrWhiteSpace(_options.ConnectionStringSecretName))
        {
            throw new InvalidOperationException("AzureStorageQueueOptions.ConnectionStringSecretName is required.");
        }

        var secret = await _secretStore.GetSecretAsync(
            new SecretIdentifier(_options.ConnectionStringSecretName),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(secret.Value))
        {
            throw new InvalidOperationException(
                $"Azure Storage Queue connection string secret '{_options.ConnectionStringSecretName}' resolved to an empty value.");
        }

        return secret.Value;
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

    private static DeadLetterWrapper? TryDeserializeWrapper(string messageText)
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

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Enqueued notification {NotificationId} to Azure Storage Queue '{Queue}'.")]
    private static partial void LogEnqueued(
        ILogger logger,
        NotificationId notificationId,
        string queue);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to deserialize message {MessageId}. Skipping.")]
    private static partial void LogDeserializeNull(ILogger logger, string messageId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Deserialization failed for message {MessageId}. Deleting poison message.")]
    private static partial void LogPoisonMessage(
        ILogger logger,
        Exception exception,
        string messageId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Dead-lettered notification {NotificationId} after {DeliveryCount} attempts. Reason: {Reason}.")]
    private static partial void LogDeadLettered(
        ILogger logger,
        NotificationId notificationId,
        int deliveryCount,
        string reason);

    /// <summary>
    /// Receives DLQ messages in batches, finds the target notification, and optionally replays it.
    /// Non-matching messages are re-enqueued into the DLQ in their original form.
    /// </summary>
    private async Task<bool> MutateDlqItemAsync(string notificationId, bool replay, CancellationToken ct)
    {
        await EnsureDlqExistsAsync(ct);
        var client = await EnsureQueueExistsAsync(ct);

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
                    await client.SendMessageAsync(json, ct);
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

    private async Task<QueueClient> EnsureQueueExistsAsync(CancellationToken ct)
    {
        if (_initialized && _client is not null)
        {
            return _client;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized && _client is not null)
            {
                return _client;
            }

            _client = await CreateQueueClientAsync(_options.QueueName, ct);

            if (_options.CreateIfNotExists)
                await _client.CreateIfNotExistsAsync(cancellationToken: ct);

            _initialized = true;
            return _client;
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

            _dlqClient = await CreateQueueClientAsync(_options.EffectiveDeadLetterQueueName, ct);

            if (_options.CreateIfNotExists)
                await _dlqClient.CreateIfNotExistsAsync(cancellationToken: ct);

            _dlqInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<QueueClient> CreateQueueClientAsync(string queueName, CancellationToken cancellationToken)
    {
        var connectionString = await ResolveConnectionStringAsync(cancellationToken);
        return new QueueClient(connectionString, queueName, new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64,
        });
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
