// <copyright file="NoOpNotificationSenderTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Test methods are self-documenting via [Fact] + method name.

using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Worker.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace HoneyDrunk.Notify.Tests.Worker;

/// <summary>
/// Tests for the worker-internal NoOpNotificationSender placeholder. Reached via
/// InternalsVisibleTo from the Worker assembly.
/// </summary>
public sealed class NoOpNotificationSenderTests
{
    [Fact]
    public async Task SendAsync_ReturnsPermanentFailure_WithNoopProvider()
    {
        var sender = new NoOpNotificationSender(NullLogger<NoOpNotificationSender>.Instance);
        var envelope = EmailEnvelope();

        var outcome = await sender.SendAsync(envelope);

        Assert.Equal(envelope.NotificationId, outcome.NotificationId);
        Assert.Equal(envelope.Channel, outcome.Channel);
        Assert.Equal("noop", outcome.Provider);
        Assert.Equal(DeliveryStatus.Failed, outcome.Status);
        Assert.Equal(FailureKind.Permanent, outcome.FailureKind);
        Assert.False(string.IsNullOrEmpty(outcome.ErrorMessage));
    }

    [Fact]
    public async Task SendAsync_NullEnvelope_Throws()
    {
        var sender = new NoOpNotificationSender(NullLogger<NoOpNotificationSender>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sender.SendAsync(null!));
    }

    private static NotificationEnvelope EmailEnvelope() =>
        new(
            NotificationId.NewId(),
            NotificationChannel.Email,
            Recipient.Email("placeholder@example.test"),
            new TemplateKey("noop.test"),
            new Dictionary<string, object?>())
        {
            CorrelationId = "test-correlation",
            TenantId = "tenant",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
}
