// <copyright file="CoverageGateBackfillTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

using AwesomeAssertions;
using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.DependencyInjection;
using HoneyDrunk.Notify.Intake;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace HoneyDrunk.Notify.Tests.Telemetry;

/// <summary>
/// Focused coverage backfill for intake behavior.
/// </summary>
public sealed partial class CoverageGateBackfillTests
{
    /// <summary>
    /// Verifies the in-memory intake queue preserves FIFO order and honors drain limits.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InMemoryNotificationEnqueuer_DrainsInFifoOrder()
    {
        // Arrange
        var enqueuer = new InMemoryNotificationEnqueuer();
        var first = Envelope(NotificationChannel.Email, "first@example.test");
        var second = Envelope(NotificationChannel.Email, "second@example.test");

        // Act
        await enqueuer.EnqueueAsync(first);
        await enqueuer.EnqueueAsync(second);
        var drained = await enqueuer.DrainAsync(1);
        var dequeued = enqueuer.TryDequeue(out var remaining);
        var empty = await enqueuer.DrainAsync(5);

        // Assert
        drained.Should().ContainSingle().Which.Should().Be(first);
        dequeued.Should().BeTrue();
        remaining.Should().Be(second);
        empty.Should().BeEmpty();
        Func<Task> nullEnvelope = () => enqueuer.EnqueueAsync(null!);
        await nullEnvelope.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies the idempotency store rejects active duplicates and reclaims expired keys.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InMemoryIdempotencyStore_RejectsDuplicatesUntilWindowExpires()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var now = DateTimeOffset.Parse("2026-05-19T16:00:00Z", null, System.Globalization.DateTimeStyles.AssumeUniversal);

        // Act
        var first = await store.TryBeginAsync("key", now, TimeSpan.FromMinutes(5));
        var duplicate = await store.TryBeginAsync("key", now.AddMinutes(1), TimeSpan.FromMinutes(5));
        var expired = await store.TryBeginAsync("key", now.AddMinutes(6), TimeSpan.FromMinutes(5));
        await store.CompleteAsync("key", "notification-1");

        // Assert
        first.Should().BeTrue();
        duplicate.Should().BeFalse();
        expired.Should().BeTrue();
        Func<Task> missingKey = () => store.TryBeginAsync(" ", now, TimeSpan.FromMinutes(5));
        Func<Task> missingCompleteKey = () => store.CompleteAsync(" ", "notification-1");
        Func<Task> missingNotification = () => store.CompleteAsync("key", " ");
        await missingKey.Should().ThrowAsync<ArgumentException>();
        await missingCompleteKey.Should().ThrowAsync<ArgumentException>();
        await missingNotification.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Verifies gateway validation, disabled runtime, duplicate idempotency, and accepted email payload paths.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task NotificationGateway_ValidatesDedupesAndBuildsEmailPayloads()
    {
        // Arrange
        var enqueuer = new InMemoryNotificationEnqueuer();
        var store = new InMemoryIdempotencyStore();
        var renderer = new StubEmailRenderer();
        var gridContext = new GridContextSnapshot(
            "honeydrunk-notify",
            "honeydrunk",
            "test",
            "corr-1",
            "cause-1",
            new TenantId("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            "project-1");
        var gateway = new NotificationGateway(
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions { DedupeWindow = TimeSpan.FromMinutes(5) }),
            enqueuer,
            store,
            renderer,
            NullLogger<NotificationGateway>.Instance,
            new FixedGridContextAccessor(gridContext));
        var request = Request("person@example.test") with { IdempotencyKey = new IdempotencyKey("same-key") };

        // Act
        var accepted = await gateway.EnqueueAsync(request);
        var duplicate = await gateway.EnqueueAsync(request);
        var invalid = await gateway.EnqueueAsync(Request("not-an-email"));
        var disabled = await new NotificationGateway(
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions { Enabled = false }),
            enqueuer,
            store,
            renderer,
            NullLogger<NotificationGateway>.Instance).EnqueueAsync(Request("person@example.test"));
        enqueuer.TryDequeue(out var envelope).Should().BeTrue();

        // Assert
        accepted.Status.Should().Be(NotificationAcceptanceStatus.Accepted);
        duplicate.RejectionReason.Should().Be(RejectionReason.DuplicateIdempotencyKey);
        invalid.RejectionReason.Should().Be(RejectionReason.ValidationFailed);
        disabled.RejectionReason.Should().Be(RejectionReason.RuntimeDisabled);
        envelope!.Payload.Should().BeOfType<EmailEnvelope>()
            .Which.Content.Subject.Should().Be("Rendered subject");
        envelope.IdempotencyKey.Should().Be(new IdempotencyKey("same-key"));
        envelope.Tags.Should().Contain("critical");
        envelope.CorrelationId.Should().Be("corr-1");
        envelope.CausationId.Should().Be("cause-1");
        envelope.NodeId.Should().Be("honeydrunk-notify");
        envelope.TenantId.Should().Be("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        envelope.Environment.Should().Be("test");
    }

    /// <summary>
    /// Verifies standalone-host fallback preserves Activity correlation when Grid context is unavailable.
    /// </summary>
    /// <param name="caseName">The fallback case under test.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Theory]
    [InlineData("null-accessor")]
    [InlineData("null-context")]
    [InlineData("uninitialized-context")]
    [InlineData("throwing-accessor")]
    public async Task NotificationGateway_FallsBackToActivityCorrelationWhenGridContextUnavailable(string caseName)
    {
        // Arrange
        using var activitySource = new ActivitySource("HoneyDrunk.Notify.Tests");
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == activitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = activitySource.StartActivity("notify-intake-test");
        var enqueuer = new InMemoryNotificationEnqueuer();
        var gateway = new NotificationGateway(
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions()),
            enqueuer,
            new InMemoryIdempotencyStore(),
            new StubEmailRenderer(),
            NullLogger<NotificationGateway>.Instance,
            AccessorFor(caseName));

        // Act
        var outcome = await gateway.EnqueueAsync(Request("person@example.test"));
        enqueuer.TryDequeue(out var envelope).Should().BeTrue();

        // Assert
        outcome.Status.Should().Be(NotificationAcceptanceStatus.Accepted);
        envelope!.CorrelationId.Should().Be(activity!.Id);
        envelope.CausationId.Should().BeNull();
        envelope.NodeId.Should().BeNull();
        envelope.TenantId.Should().BeNull();
        envelope.Environment.Should().BeNull();
    }

    /// <summary>
    /// Verifies standalone DI composition resolves the gateway and preserves activity fallback without Grid context.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AddHoneyDrunkNotifyRuntime_ResolvesGatewayWithoutGridContextAccessorAndUsesActivityFallback()
    {
        // Arrange
        using var activitySource = new ActivitySource("HoneyDrunk.Notify.Tests.DI");
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == activitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        await using var provider = BuildNotifyProvider();
        var gateway = provider.GetRequiredService<INotificationGateway>();
        var enqueuer = provider.GetRequiredService<INotificationEnqueuer>()
            .Should()
            .BeOfType<InMemoryNotificationEnqueuer>()
            .Subject;

        // Act
        using var activity = activitySource.StartActivity("notify-intake-di-test");
        var outcome = await gateway.EnqueueAsync(SmsRequest());
        enqueuer.TryDequeue(out var envelope).Should().BeTrue();

        // Assert
        outcome.Status.Should().Be(NotificationAcceptanceStatus.Accepted);
        envelope!.CorrelationId.Should().Be(activity!.Id);
        envelope.CausationId.Should().BeNull();
        envelope.NodeId.Should().BeNull();
        envelope.TenantId.Should().BeNull();
        envelope.Environment.Should().BeNull();
    }

    /// <summary>
    /// Verifies Grid-host DI composition stamps current Grid context onto accepted envelopes.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AddHoneyDrunkNotifyRuntime_StampsGridContextWhenAccessorRegistered()
    {
        // Arrange
        var tenantId = new TenantId("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var gridContext = new GridContextSnapshot(
            "honeydrunk-notify",
            "honeydrunk",
            "test",
            "corr-1",
            "cause-1",
            tenantId,
            "project-1");

        await using var provider = BuildNotifyProvider(new FixedGridContextAccessor(gridContext));
        var gateway = provider.GetRequiredService<INotificationGateway>();
        var enqueuer = provider.GetRequiredService<INotificationEnqueuer>()
            .Should()
            .BeOfType<InMemoryNotificationEnqueuer>()
            .Subject;

        // Act
        var outcome = await gateway.EnqueueAsync(SmsRequest());
        enqueuer.TryDequeue(out var envelope).Should().BeTrue();

        // Assert
        outcome.Status.Should().Be(NotificationAcceptanceStatus.Accepted);
        envelope!.CorrelationId.Should().Be("corr-1");
        envelope.CausationId.Should().Be("cause-1");
        envelope.NodeId.Should().Be("honeydrunk-notify");
        envelope.TenantId.Should().Be(tenantId.ToString());
        envelope.Environment.Should().Be("test");
    }

    private static IGridContextAccessor? AccessorFor(string caseName) =>
        caseName switch
        {
            "null-accessor" => null,
            "null-context" => new NullGridContextAccessor(),
            "uninitialized-context" => new FixedGridContextAccessor(UninitializedGridContext.Instance),
            "throwing-accessor" => new ThrowingGridContextAccessor(),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, null),
        };

    private static ServiceProvider BuildNotifyProvider(IGridContextAccessor? gridContextAccessor = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (gridContextAccessor is not null)
        {
            services.AddSingleton(gridContextAccessor);
        }

        services.AddHoneyDrunkNotifyRuntime();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static NotificationRequest SmsRequest() =>
        new(NotificationChannel.Sms, new Recipient(NotificationChannel.Sms, "+15555550100"), new TemplateKey("sms-alert"), new Dictionary<string, object?>());

    private sealed class FixedGridContextAccessor(IGridContext gridContext) : IGridContextAccessor
    {
        public IGridContext GridContext { get; } = gridContext;
    }

    private sealed class NullGridContextAccessor : IGridContextAccessor
    {
        public IGridContext GridContext => null!;
    }

    private sealed class ThrowingGridContextAccessor : IGridContextAccessor
    {
        public IGridContext GridContext => throw new InvalidOperationException("Grid context is not initialized.");
    }

    private sealed class UninitializedGridContext : IGridContext
    {
        public static readonly UninitializedGridContext Instance = new();

        public bool IsInitialized => false;

        public string CorrelationId => throw new InvalidOperationException();

        public string? CausationId => throw new InvalidOperationException();

        public string NodeId => throw new InvalidOperationException();

        public string StudioId => throw new InvalidOperationException();

        public string Environment => throw new InvalidOperationException();

        public TenantId TenantId => throw new InvalidOperationException();

        public string? ProjectId => throw new InvalidOperationException();

        public CancellationToken Cancellation => throw new InvalidOperationException();

        public IReadOnlyDictionary<string, string> Baggage => throw new InvalidOperationException();

        public DateTimeOffset CreatedAtUtc => throw new InvalidOperationException();

        public void AddBaggage(string key, string value) => throw new InvalidOperationException();
    }
}
