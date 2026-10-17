using AwesomeAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.DependencyInjection;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Queue.InMemory;
using HoneyDrunk.Notify.Queue.InMemory.DependencyInjection;
using HoneyDrunk.Notify.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Notify.IntegrationTests;

/// <summary>
/// Verifies that messages exceeding <see cref="NotificationQueueOptions.MaxDeliveryAttempts"/>
/// are moved to the dead-letter queue instead of being abandoned for redelivery.
/// </summary>
public sealed class PoisonMessageDeadLetterTests
{
    private const int MaxAttempts = 3;

    /// <summary>
    /// Verifies that messages exceeding max delivery attempts are dead-lettered.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Message_exceeding_max_delivery_attempts_is_dead_lettered()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHoneyDrunkNotifyRuntime(opts =>
        {
            opts.MaxAttempts = 1;
            opts.BaseDelay = TimeSpan.Zero;
            opts.EnableDedupe = false;
        });

        services.AddHoneyDrunkNotifyInMemoryQueue(opts =>
        {
            opts.MaxDeliveryAttempts = MaxAttempts;
        });

        services.AddSingleton<INotificationSender, AlwaysTransientFailureSender>();

        await using var provider = services.BuildServiceProvider();

        var queue = provider.GetRequiredService<INotificationQueue>();
        var dispatcher = provider.GetRequiredService<NotificationDispatcher>();
        var queueOptions = MaxAttempts;

        var envelope = new NotificationEnvelope(
            NotificationId.NewId(),
            NotificationChannel.Email,
            new Recipient(NotificationChannel.Email, "test@example.com"),
            new TemplateKey("test-template"),
            new Dictionary<string, object?> { ["name"] = "Test" });

        await queue.EnqueueAsync(envelope);

        for (var cycle = 1; cycle <= MaxAttempts; cycle++)
        {
            var batch = await queue.DequeueBatchAsync(1);
            batch.Should().HaveCount(1, "each cycle should dequeue the same message");

            var item = batch[0];
            item.DeliveryCount.Should().Be(cycle);

            var outcome = await dispatcher.DispatchAsync(item.Envelope);

            outcome.Status.Should().Be(DeliveryStatus.Failed);
            outcome.FailureKind.Should().Be(FailureKind.Transient);

            if (item.DeliveryCount >= queueOptions)
            {
                await queue.DeadLetterAsync(item, "Max delivery attempts exceeded");
            }
            else
            {
                await queue.AbandonAsync(item);
            }
        }

        var afterDlq = await queue.DequeueBatchAsync(1);
        afterDlq.Should().BeEmpty("message should be removed from the main queue after dead-lettering");

        var inMemoryQueue = (InMemoryNotificationQueue)queue;
        inMemoryQueue.DeadLetters.Should().HaveCount(1);

        var dlqItem = inMemoryQueue.DeadLetters[0];
        dlqItem.Envelope.NotificationId.Should().Be(envelope.NotificationId);
        dlqItem.Reason.Should().Contain("Max delivery attempts");
        dlqItem.DeliveryCount.Should().Be(MaxAttempts);
    }

    /// <summary>
    /// Verifies that messages below max attempts are redelivered, not dead-lettered.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Message_below_max_attempts_is_redelivered_not_dead_lettered()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHoneyDrunkNotifyRuntime(opts =>
        {
            opts.MaxAttempts = 1;
            opts.BaseDelay = TimeSpan.Zero;
            opts.EnableDedupe = false;
        });

        services.AddHoneyDrunkNotifyInMemoryQueue(opts =>
        {
            opts.MaxDeliveryAttempts = MaxAttempts;
        });

        services.AddSingleton<INotificationSender, AlwaysTransientFailureSender>();

        await using var provider = services.BuildServiceProvider();

        var queue = provider.GetRequiredService<INotificationQueue>();
        var dispatcher = provider.GetRequiredService<NotificationDispatcher>();

        var envelope = new NotificationEnvelope(
            NotificationId.NewId(),
            NotificationChannel.Email,
            new Recipient(NotificationChannel.Email, "test@example.com"),
            new TemplateKey("test-template"),
            new Dictionary<string, object?> { ["name"] = "Test" });

        await queue.EnqueueAsync(envelope);

        var batch = await queue.DequeueBatchAsync(1);
        var item = batch[0];
        item.DeliveryCount.Should().Be(1);

        var outcome = await dispatcher.DispatchAsync(item.Envelope);
        outcome.FailureKind.Should().Be(FailureKind.Transient);

        item.DeliveryCount.Should().BeLessThan(MaxAttempts);
        await queue.AbandonAsync(item);

        var reBatch = await queue.DequeueBatchAsync(1);
        reBatch.Should().HaveCount(1, "abandoned message should be available for redelivery");
        reBatch[0].DeliveryCount.Should().Be(2);

        var inMemoryQueue = (InMemoryNotificationQueue)queue;
        inMemoryQueue.DeadLetters.Should().BeEmpty("message is below threshold and should not be dead-lettered");
    }

#pragma warning disable CA1812
    private sealed class AlwaysTransientFailureSender : INotificationSender
#pragma warning restore CA1812
    {
        public Task<DeliveryOutcome> SendAsync(NotificationEnvelope envelope, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeliveryOutcome.Failed(
                envelope.NotificationId,
                AttemptId.NewId(),
                envelope.Channel,
                "test-provider",
                FailureKind.Transient,
                "Simulated transient failure"));
        }
    }
}
