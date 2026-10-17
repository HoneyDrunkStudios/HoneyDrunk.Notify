using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Queue.AzureStorage;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace HoneyDrunk.Notify.Tests.Queue;

/// <summary>Exercises delivery and dead-letter behavior through the Azure client boundary.</summary>
public sealed class AzureStorageQueueDeliveryTests
{
    /// <summary>Serialized envelopes preserve ownership and malformed messages are handled separately.</summary>
    /// <returns>The test task.</returns>
    [Fact]
    public async Task EnqueueAndDequeuePreserveContextAndDeletePoisonMessages()
    {
        var main = CreateClient();
        var dead = CreateClient();
        await using var queue = CreateQueue(main, dead);
        var envelope = CreateEnvelope();
        string? serialized = null;
        _ = main.SendMessageAsync(Arg.Do<string>(value => serialized = value), Arg.Any<CancellationToken>());

        await queue.EnqueueAsync(envelope);
        Assert.NotNull(serialized);
        main.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(new[] { Message("valid", serialized), Message("poison", "invalid-json"), Message("null", "null"), Message("invalid-template", serialized.Replace("welcome", string.Empty, StringComparison.Ordinal)) }, Substitute.For<Response>()));

        var delivered = Assert.Single(await queue.DequeueBatchAsync(100));
        Assert.Equal(envelope.NotificationId, delivered.Envelope.NotificationId);
        Assert.Equal(envelope.TemplateKey, delivered.Envelope.TemplateKey);
        Assert.Equal(envelope.TenantId, delivered.Envelope.TenantId);
        Assert.Equal(envelope.CorrelationId, delivered.Envelope.CorrelationId);
        Assert.Equal("valid|receipt", delivered.Receipt);
        Assert.Equal(2, delivered.DeliveryCount);
        await main.Received(1).DeleteMessageAsync("poison", "receipt", Arg.Any<CancellationToken>());
        await main.Received(1).DeleteMessageAsync("invalid-template", "receipt", Arg.Any<CancellationToken>());
        await main.DidNotReceive().DeleteMessageAsync("valid", Arg.Any<string>(), Arg.Any<CancellationToken>());

        await queue.CompleteAsync(delivered);
        await main.Received(1).DeleteMessageAsync("valid", "receipt", Arg.Any<CancellationToken>());
        await queue.AbandonAsync(delivered);
        await main.Received(1).UpdateMessageAsync("valid", "receipt", Arg.Any<string>(), TimeSpan.FromSeconds(1), Arg.Any<CancellationToken>());
        await main.Received(1).CreateIfNotExistsAsync(Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Dead-letter replay sends only the matching envelope and returns unrelated messages.</summary>
    /// <param name="replay">Whether to replay rather than purge.</param>
    /// <returns>The test task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeadLetterInspectionAndMutationPreserveUnrelatedMessages(bool replay)
    {
        var main = CreateClient();
        var dead = CreateClient();
        await using var queue = CreateQueue(main, dead);
        var envelope = CreateEnvelope();
        var item = new QueuedNotification(envelope, "original|receipt", DateTimeOffset.UtcNow, 3);
        string? wrapper = null;
        _ = dead.SendMessageAsync(Arg.Do<string>(value => wrapper = value), Arg.Any<CancellationToken>());
        await queue.DeadLetterAsync(item, "retry-limit");
        Assert.NotNull(wrapper);
        await main.Received(1).DeleteMessageAsync("original", "receipt", Arg.Any<CancellationToken>());
        dead.PeekMessagesAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(Response.FromValue(
            new[] { Peek("target", wrapper), Peek("invalid", "invalid-json") }, Substitute.For<Response>()));

        var listed = Assert.Single(await queue.ListAsync(100));
        Assert.Equal(envelope.NotificationId.ToString(), listed.NotificationId);
        Assert.Equal("retry-limit", listed.Reason);
        Assert.Equal(3, listed.DeliveryCount);
        Assert.NotNull(listed.DeadLetteredAt);
        Assert.NotNull(await queue.FindByNotificationIdAsync(envelope.NotificationId.ToString()));
        Assert.Null(await queue.FindByNotificationIdAsync("missing"));
        dead.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()).Returns(Response.FromValue(
            new[] { Message("target", wrapper), Message("other", "invalid-json") }, Substitute.For<Response>()));

        var found = replay
            ? await queue.ReplayAsync(envelope.NotificationId.ToString())
            : await queue.PurgeAsync(envelope.NotificationId.ToString());

        Assert.True(found);
        await dead.Received(1).DeleteMessageAsync("target", "receipt", Arg.Any<CancellationToken>());
        await dead.Received(1).UpdateMessageAsync("other", "receipt", Arg.Any<string>(), TimeSpan.FromSeconds(1), Arg.Any<CancellationToken>());
        if (replay)
        {
            await main.Received(1).SendMessageAsync(Arg.Is<string>(value => value.Contains("tenant-a", StringComparison.Ordinal)), Arg.Any<CancellationToken>());
        }
        else
        {
            await main.DidNotReceive().SendMessageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    /// <summary>Empty queues, missing items and invalid receipts have deterministic outcomes.</summary>
    /// <returns>The test task.</returns>
    [Fact]
    public async Task EmptyQueuesAndInvalidReceiptsDoNotDeleteMessages()
    {
        var main = CreateClient();
        var dead = CreateClient();
        await using var queue = CreateQueue(main, dead, createIfMissing: false);
        Assert.Empty(await queue.DequeueBatchAsync(1));
        Assert.Empty(await queue.ListAsync(1));
        Assert.Null(await queue.FindByNotificationIdAsync("missing"));
        Assert.False(await queue.ReplayAsync("missing"));
        Assert.False(await queue.PurgeAsync("missing"));
        var item = new QueuedNotification(CreateEnvelope(), "invalid", DateTimeOffset.UtcNow, 1);
        await Assert.ThrowsAsync<FormatException>(() => queue.CompleteAsync(item));
        await main.DidNotReceive().DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await main.DidNotReceive().CreateIfNotExistsAsync(Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>());
        await queue.DisposeAsync();
    }

    private static AzureStorageNotificationQueue CreateQueue(QueueClient main, QueueClient dead, bool createIfMissing = true) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new AzureStorageQueueOptions { ConnectionString = "local-test", QueueName = "notify", CreateIfNotExists = createIfMissing }),
            NullLogger<AzureStorageNotificationQueue>.Instance,
            (_, name) => name == "notify" ? main : dead);

    private static QueueClient CreateClient()
    {
        var client = Substitute.For<QueueClient>();
        client.CreateIfNotExistsAsync(Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(Substitute.For<Response>());
        client.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Substitute.For<Response>());
        client.SendMessageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Response.FromValue(
            QueuesModelFactory.SendReceipt("sent", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), "receipt", DateTimeOffset.UtcNow), Substitute.For<Response>()));
        client.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()).Returns(Response.FromValue(Array.Empty<QueueMessage>(), Substitute.For<Response>()));
        client.PeekMessagesAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(Response.FromValue(Array.Empty<PeekedMessage>(), Substitute.For<Response>()));
        return client;
    }

    private static QueueMessage Message(string id, string json) => QueuesModelFactory.QueueMessage(id, "receipt", json, 2, null, null, null);

    private static PeekedMessage Peek(string id, string json) => QueuesModelFactory.PeekedMessage(id, json, 2, null, null);

    private static NotificationEnvelope CreateEnvelope() => new(NotificationId.NewId(), NotificationChannel.Email, new Recipient(NotificationChannel.Email, "recipient@example.test"), new TemplateKey("welcome"), new Dictionary<string, object?>())
    {
        TenantId = "tenant-a",
        CorrelationId = "correlation-a",
    };
}
