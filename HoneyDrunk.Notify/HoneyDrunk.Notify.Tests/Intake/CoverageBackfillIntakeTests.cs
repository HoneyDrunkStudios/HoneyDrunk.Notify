// <copyright file="CoverageGateBackfillTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

using AwesomeAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.Intake;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Storage;
using Microsoft.Extensions.Logging.Abstractions;

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
        var gateway = new NotificationGateway(
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions { DedupeWindow = TimeSpan.FromMinutes(5) }),
            enqueuer,
            store,
            renderer,
            NullLogger<NotificationGateway>.Instance);
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
    }
}
