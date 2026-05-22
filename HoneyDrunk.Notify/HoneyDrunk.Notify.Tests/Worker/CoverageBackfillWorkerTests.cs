// <copyright file="CoverageGateBackfillTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

using AwesomeAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.DependencyInjection;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Queue.InMemory.DependencyInjection;
using HoneyDrunk.Notify.Routing;
using HoneyDrunk.Notify.Worker.Hosting;
using HoneyDrunk.Notify.Worker.Options;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HoneyDrunk.Notify.Tests.Telemetry;

/// <summary>
/// Focused coverage backfill for worker behavior.
/// </summary>
public sealed partial class CoverageGateBackfillTests
{
    /// <summary>
    /// Verifies the worker dispatcher completes, abandons, and dead-letters queue items by outcome.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task WorkerDispatcher_ProcessesQueueItemsByDeliveryOutcome()
    {
        // Arrange
        var completed = Envelope(NotificationChannel.Email, "completed@example.test");
        var abandoned = Envelope(NotificationChannel.Email, "abandoned@example.test");
        var deadLettered = Envelope(NotificationChannel.Email, "deadletter@example.test");
        var permanent = Envelope(NotificationChannel.Email, "permanent@example.test");
        var queue = new RecordingNotificationQueue(
            new QueuedNotification(completed, "complete", DateTimeOffset.UtcNow),
            new QueuedNotification(abandoned, "abandon", DateTimeOffset.UtcNow),
            new QueuedNotification(deadLettered, "deadletter", DateTimeOffset.UtcNow, 5),
            new QueuedNotification(permanent, "permanent", DateTimeOffset.UtcNow));
        var sender = new SequenceSender(
            Success(completed),
            Transient(abandoned),
            Transient(deadLettered),
            Permanent(permanent));
        using var service = new NotifyDispatcherBackgroundService(
            queue,
            Dispatcher(sender, maxAttempts: 1),
            Microsoft.Extensions.Options.Options.Create(new NotifyWorkerOptions
            {
                Enabled = true,
                BatchSize = 10,
                PollInterval = TimeSpan.FromMinutes(5),
            }),
            Microsoft.Extensions.Options.Options.Create(new NotificationQueueOptions { MaxDeliveryAttempts = 5 }),
            NullLogger<NotifyDispatcherBackgroundService>.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);
        await queue.Processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        // Assert
        queue.Completed.Should().ContainSingle(item => item.Envelope.NotificationId == completed.NotificationId);
        queue.Abandoned.Should().ContainSingle(item => item.Envelope.NotificationId == abandoned.NotificationId);
        queue.DeadLettered.Should().ContainSingle(entry => entry.queuedNotification.Envelope.NotificationId == deadLettered.NotificationId);
        queue.Completed.Should().Contain(item => item.Envelope.NotificationId == permanent.NotificationId);
    }

    /// <summary>
    /// Verifies the in-memory queue covers delivery, dead-letter, replay, and purge paths.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InMemoryQueue_TracksDeliveryAndDeadLetterLifecycle()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHoneyDrunkNotifyInMemoryQueue(options => options.MaxBatchSize = 2);
        using var provider = services.BuildServiceProvider();
        var queue = provider.GetRequiredService<INotificationQueue>();
        var inspector = provider.GetRequiredService<IDeadLetterInspector>();
        var first = Envelope(NotificationChannel.Email, "first@example.test");
        var second = Envelope(NotificationChannel.Email, "second@example.test");
        var third = Envelope(NotificationChannel.Email, "third@example.test");

        // Act
        await queue.EnqueueAsync(first);
        await queue.EnqueueAsync(second);
        await queue.EnqueueAsync(third);
        var firstBatch = await queue.DequeueBatchAsync(10);
        await queue.CompleteAsync(firstBatch[0]);
        await queue.AbandonAsync(firstBatch[1]);
        var secondBatch = await queue.DequeueBatchAsync(10);
        var replayTarget = secondBatch.Single(item => item.Envelope.NotificationId == second.NotificationId);
        await queue.DeadLetterAsync(replayTarget, "provider failed");
        var listed = await inspector.ListAsync(10);
        var found = await inspector.FindByNotificationIdAsync(second.NotificationId.ToString());
        var missing = await inspector.FindByNotificationIdAsync("missing");
        var replayed = await inspector.ReplayAsync(second.NotificationId.ToString());
        var replayedAgain = await inspector.ReplayAsync(second.NotificationId.ToString());
        var afterReplay = await queue.DequeueBatchAsync(10);
        await queue.DeadLetterAsync(afterReplay.Single(item => item.Envelope.NotificationId == second.NotificationId), "still failed");
        var purged = await inspector.PurgeAsync(second.NotificationId.ToString());
        var purgedAgain = await inspector.PurgeAsync(second.NotificationId.ToString());
        Func<Task> nullEnvelope = () => queue.EnqueueAsync(null!);
        Func<Task> nullComplete = () => queue.CompleteAsync(null!);
        Func<Task> nullAbandon = () => queue.AbandonAsync(null!);
        Func<Task> nullDeadLetter = () => queue.DeadLetterAsync(null!, "failed");
        Func<Task> blankReason = () => queue.DeadLetterAsync(firstBatch[0], " ");

        // Assert
        firstBatch.Should().HaveCount(2);
        secondBatch.Should().HaveCount(2);
        listed.Should().ContainSingle(entry => entry.NotificationId == second.NotificationId.ToString());
        found.Should().NotBeNull();
        found!.Reason.Should().Be("provider failed");
        found.Channel.Should().Be(NotificationChannel.Email.ToString());
        found.TemplateKey.Should().Be("welcome");
        found.CorrelationId.Should().Be("corr-1");
        found.TenantId.Should().Be("tenant-1");
        found.DeadLetteredAt.Should().NotBeNull();
        missing.Should().BeNull();
        replayed.Should().BeTrue();
        replayedAgain.Should().BeFalse();
        afterReplay.Should().Contain(item => item.Envelope.NotificationId == second.NotificationId);
        purged.Should().BeTrue();
        purgedAgain.Should().BeFalse();
        await nullEnvelope.Should().ThrowAsync<ArgumentNullException>();
        await nullComplete.Should().ThrowAsync<ArgumentNullException>();
        await nullAbandon.Should().ThrowAsync<ArgumentNullException>();
        await nullDeadLetter.Should().ThrowAsync<ArgumentNullException>();
        await blankReason.Should().ThrowAsync<ArgumentException>();
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());
        services.AddHoneyDrunkNotifyRuntime();
        register(services);
        return services.BuildServiceProvider();
    }

    private static NotificationDispatcher Dispatcher(SequenceSender sender, int maxAttempts) =>
        new(
            new StubResolver(sender),
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions
            {
                MaxAttempts = maxAttempts,
                BaseDelay = TimeSpan.Zero,
                MaxDelay = TimeSpan.Zero,
            }),
            new ExponentialBackoffStrategy(),
            TimeProvider.System,
            NullLogger<NotificationDispatcher>.Instance);

    private static NotificationRequest Request(string address) =>
        new(NotificationChannel.Email, Recipient.Email(address), new TemplateKey("welcome"), new Dictionary<string, object?> { ["name"] = "Oleg" })
        {
            Priority = NotificationPriority.High,
            Tags = ["critical"],
        };

    private static NotificationEnvelope Envelope(NotificationChannel channel, string address) =>
        new(NotificationId.NewId(), channel, new Recipient(channel, address), new TemplateKey("welcome"), new Dictionary<string, object?>())
        {
            CorrelationId = "corr-1",
            TenantId = "tenant-1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

    private static DeliveryOutcome Success(NotificationEnvelope envelope) =>
        DeliveryOutcome.Succeeded(envelope.NotificationId, AttemptId.NewId(), envelope.Channel, "test");

    private static DeliveryOutcome Transient(NotificationEnvelope envelope) =>
        DeliveryOutcome.Failed(
            envelope.NotificationId,
            AttemptId.NewId(),
            envelope.Channel,
            "test",
            FailureKind.Transient,
            "retry later");

    private static DeliveryOutcome Permanent(NotificationEnvelope envelope) =>
        DeliveryOutcome.Failed(
            envelope.NotificationId,
            AttemptId.NewId(),
            envelope.Channel,
            "test",
            FailureKind.Permanent,
            "do not retry");

    private sealed class StubEmailRenderer : IEmailTemplateRenderer
    {
        public Task<EmailContent> RenderEmailAsync(
            TemplateKey templateKey,
            IReadOnlyDictionary<string, object?> model,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmailContent("Rendered subject", "Rendered body"));
    }

    private sealed class StubResolver(INotificationSender sender) : INotificationSenderResolver
    {
        public INotificationSender Resolve(NotificationChannel channel) => sender;
    }

    private sealed class RecordingNotificationQueue(params QueuedNotification[] batch) : INotificationQueue
    {
        private bool _dequeued;

        public TaskCompletionSource Processed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<QueuedNotification> Completed { get; } = [];

        public List<QueuedNotification> Abandoned { get; } = [];

        public List<(QueuedNotification queuedNotification, string reason)> DeadLettered { get; } = [];

        public Task EnqueueAsync(NotificationEnvelope envelope, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<QueuedNotification>> DequeueBatchAsync(int max, CancellationToken ct = default)
        {
            if (_dequeued)
                return Task.FromResult<IReadOnlyList<QueuedNotification>>([]);

            _dequeued = true;
            return Task.FromResult<IReadOnlyList<QueuedNotification>>(batch.Take(max).ToArray());
        }

        public Task CompleteAsync(QueuedNotification item, CancellationToken ct = default)
        {
            Completed.Add(item);
            TrySignalProcessed();
            return Task.CompletedTask;
        }

        public Task AbandonAsync(QueuedNotification item, CancellationToken ct = default)
        {
            Abandoned.Add(item);
            TrySignalProcessed();
            return Task.CompletedTask;
        }

        public Task DeadLetterAsync(QueuedNotification item, string reason, CancellationToken ct = default)
        {
            DeadLettered.Add((item, reason));
            TrySignalProcessed();
            return Task.CompletedTask;
        }

        private void TrySignalProcessed()
        {
            if (Completed.Count + Abandoned.Count + DeadLettered.Count >= batch.Length)
                Processed.TrySetResult();
        }
    }

    private sealed class SequenceSender(params DeliveryOutcome[] outcomes) : INotificationSender
    {
        public int Calls { get; private set; }

        public Task<DeliveryOutcome> SendAsync(NotificationEnvelope envelope, CancellationToken cancellationToken = default)
        {
            var index = Math.Min(Calls, outcomes.Length - 1);
            Calls++;
            return Task.FromResult(outcomes[index]);
        }
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        public Task<SecretValue> GetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SecretValue(identifier, "secret-value", version: null));

        public Task<VaultResult<SecretValue>> TryGetSecretAsync(
            SecretIdentifier identifier,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SecretVersion>> ListSecretVersionsAsync(
            string secretName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
