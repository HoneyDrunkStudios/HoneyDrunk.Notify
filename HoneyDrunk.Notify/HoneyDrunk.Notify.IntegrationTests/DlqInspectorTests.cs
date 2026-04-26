using FluentAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Queue.InMemory.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Notify.IntegrationTests;

/// <summary>
/// End-to-end tests for DLQ inspection, replay, and purge operations
/// using the InMemory queue adapter.
/// </summary>
public sealed class DlqInspectorTests
{
    /// <summary>
    /// Verifies that a seeded DLQ item appears in the list.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task List_ReturnsSeededItem()
    {
        var (inspector, queue) = await BuildAsync();
        var envelope = CreateEnvelope();

        await SeedDlqAsync(queue, envelope);

        var entries = await inspector.ListAsync(10);

        entries.Should().HaveCount(1);
        entries[0].NotificationId.Should().Be(envelope.NotificationId.ToString());
        entries[0].TemplateKey.Should().Be("test-template");
        entries[0].Channel.Should().Be("Email");
        entries[0].DeliveryCount.Should().Be(1);
        entries[0].DeadLetteredAt.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that replaying moves an item from the DLQ back to the main queue.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Replay_MovesItemFromDlqToMainQueue()
    {
        var (inspector, queue) = await BuildAsync();
        var envelope = CreateEnvelope();
        var notificationId = envelope.NotificationId.ToString();

        await SeedDlqAsync(queue, envelope);

        var replayed = await inspector.ReplayAsync(notificationId);
        replayed.Should().BeTrue();

        var afterReplay = await inspector.ListAsync(10);
        afterReplay.Should().BeEmpty("item should be removed from DLQ after replay");

        var mainBatch = await queue.DequeueBatchAsync(1);
        mainBatch.Should().HaveCount(1, "replayed item should appear on the main queue");
        mainBatch[0].Envelope.NotificationId.Should().Be(envelope.NotificationId);
    }

    /// <summary>
    /// Verifies that purging removes an item from the DLQ without replaying it.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Purge_RemovesItemFromDlqWithoutReplay()
    {
        var (inspector, queue) = await BuildAsync();
        var envelope = CreateEnvelope();
        var notificationId = envelope.NotificationId.ToString();

        await SeedDlqAsync(queue, envelope);

        var purged = await inspector.PurgeAsync(notificationId);
        purged.Should().BeTrue();

        var afterPurge = await inspector.ListAsync(10);
        afterPurge.Should().BeEmpty("item should be removed from DLQ after purge");

        var mainBatch = await queue.DequeueBatchAsync(1);
        mainBatch.Should().BeEmpty("purged item should NOT appear on the main queue");
    }

    /// <summary>
    /// Verifies that FindByNotificationId returns a DLQ entry with the expected details.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task FindByNotificationId_ReturnsDlqEntryWithDetails()
    {
        var (inspector, queue) = await BuildAsync();
        var envelope = CreateEnvelope();
        var notificationId = envelope.NotificationId.ToString();

        await SeedDlqAsync(queue, envelope);

        var entry = await inspector.FindByNotificationIdAsync(notificationId);

        entry.Should().NotBeNull();
        entry!.NotificationId.Should().Be(notificationId);
        entry.Reason.Should().Contain("Test dead-letter reason");
        entry.CorrelationId.Should().Be("corr-dlq-test");
        entry.TenantId.Should().Be("tenant-1");
    }

    /// <summary>
    /// Verifies that replaying a nonexistent notification returns false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Replay_ReturnsFalse_WhenNotFound()
    {
        var (inspector, _) = await BuildAsync();

        var result = await inspector.ReplayAsync("nonexistent-id");

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that purging a nonexistent notification returns false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Purge_ReturnsFalse_WhenNotFound()
    {
        var (inspector, _) = await BuildAsync();

        var result = await inspector.PurgeAsync("nonexistent-id");

        result.Should().BeFalse();
    }

    private static NotificationEnvelope CreateEnvelope(string templateKey = "test-template") =>
        new(
            NotificationId.NewId(),
            NotificationChannel.Email,
            new Recipient(NotificationChannel.Email, "user@example.com"),
            new TemplateKey(templateKey),
            new Dictionary<string, object?> { ["name"] = "Test" })
        {
            CorrelationId = "corr-dlq-test",
            TenantId = "tenant-1",
        };

    private static async Task<(IDeadLetterInspector inspector, INotificationQueue queue)> BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkNotifyInMemoryQueue(o => o.MaxDeliveryAttempts = 1);

        var provider = services.BuildServiceProvider();

        var queue = provider.GetRequiredService<INotificationQueue>();
        var inspector = provider.GetRequiredService<IDeadLetterInspector>();

        return await Task.FromResult((inspector, queue));
    }

    private static async Task SeedDlqAsync(INotificationQueue queue, NotificationEnvelope envelope)
    {
        await queue.EnqueueAsync(envelope);
        var batch = await queue.DequeueBatchAsync(1);
        await queue.DeadLetterAsync(batch[0], "Test dead-letter reason");
    }
}
