using FluentAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.DependencyInjection;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Queue.InMemory.DependencyInjection;
using HoneyDrunk.Notify.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace HoneyDrunk.Notify.IntegrationTests;

/// <summary>
/// Proves that correlation flows end-to-end from request intake through delivery,
/// and that the fake sender receives the envelope with correct provider/status.
/// </summary>
public sealed class CorrelationCanaryTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<Activity> _capturedActivities = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationCanaryTests"/> class.
    /// </summary>
    public CorrelationCanaryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "HoneyDrunk.Notify",
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _capturedActivities.Enqueue(activity),
        };

        ActivitySource.AddActivityListener(_listener);
    }

    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();

    /// <summary>
    /// Verifies that correlation flows end-to-end from enqueue through dispatch.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Enqueue_and_dispatch_carries_correlation_through_envelope()
    {
        var fakeSender = new FakeNotificationSender();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHoneyDrunkNotifyRuntime(
            opts =>
            {
                opts.MaxAttempts = 1;
                opts.BaseDelay = TimeSpan.Zero;
                opts.EnableDedupe = false;
            },
            templates =>
            {
                templates.RootPath = Path.Join(Path.GetTempPath(), "hd-canary-" + Guid.NewGuid().ToString("N"));
            });

        services.AddHoneyDrunkNotifyInMemoryQueue();
        services.AddSingleton<INotificationSender>(fakeSender);

        await using var provider = services.BuildServiceProvider();

        var queue = provider.GetRequiredService<INotificationQueue>();
        var dispatcher = provider.GetRequiredService<NotificationDispatcher>();

        var correlationId = $"canary-{Guid.NewGuid():N}";

        var envelope = new NotificationEnvelope(
            NotificationId.NewId(),
            NotificationChannel.Email,
            new Recipient(NotificationChannel.Email, "canary@test.com"),
            new TemplateKey("canary-template"),
            new Dictionary<string, object?> { ["Name"] = "CanaryTest" })
        {
            CorrelationId = correlationId,
        };

        await queue.EnqueueAsync(envelope);

        var batch = await queue.DequeueBatchAsync(1);
        batch.Should().HaveCount(1);

        var item = batch[0];
        var outcome = await dispatcher.DispatchAsync(item.Envelope);
        await queue.CompleteAsync(item);

        fakeSender.ReceivedEnvelopes.Should().HaveCount(1, "sender should be called exactly once");

        var receivedEnvelope = fakeSender.ReceivedEnvelopes[0];
        receivedEnvelope.CorrelationId.Should().Be(correlationId, "correlation ID must flow through the envelope");
        receivedEnvelope.NotificationId.Should().Be(envelope.NotificationId);
        receivedEnvelope.Channel.Should().Be(NotificationChannel.Email);

        outcome.Provider.Should().Be("fake");
        outcome.Status.Should().Be(DeliveryStatus.Succeeded);

        var capturedActivities = _capturedActivities.ToArray();

        capturedActivities.Should().Contain(
            a =>
                a.DisplayName == NotifyEventNames.DispatchAttempt ||
                a.OperationName == NotifyEventNames.DispatchAttempt,
            "a dispatch Activity span should have been emitted");
    }

#pragma warning disable CA1812
    private sealed class FakeNotificationSender : INotificationSender
#pragma warning restore CA1812
    {
        internal List<NotificationEnvelope> ReceivedEnvelopes { get; } = [];

        public Task<DeliveryOutcome> SendAsync(NotificationEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ReceivedEnvelopes.Add(envelope);

            return Task.FromResult(DeliveryOutcome.Succeeded(
                envelope.NotificationId,
                AttemptId.NewId(),
                envelope.Channel,
                "fake"));
        }
    }
}
