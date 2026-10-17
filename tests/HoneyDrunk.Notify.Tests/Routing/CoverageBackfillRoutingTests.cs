// <copyright file="CoverageGateBackfillTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

using AwesomeAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Notify.Tests.Telemetry;

/// <summary>
/// Focused coverage backfill for routing behavior.
/// </summary>
public sealed partial class CoverageGateBackfillTests
{
    /// <summary>
    /// Verifies exponential backoff doubles delays and respects the configured cap.
    /// </summary>
    [Fact]
    public void ExponentialBackoffStrategy_CalculatesAndCapsDelays()
    {
        // Arrange
        var strategy = new ExponentialBackoffStrategy();

        // Act / Assert
        strategy.Calculate(0, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)).Should().Be(TimeSpan.FromSeconds(2));
        strategy.Calculate(2, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)).Should().Be(TimeSpan.FromSeconds(8));
        strategy.Calculate(10, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)).Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Verifies dispatcher terminal paths for success, permanent failure, deferred retry, and invalid retry configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task NotificationDispatcher_HandlesTerminalAndRetryOutcomes()
    {
        // Arrange
        var envelope = Envelope(NotificationChannel.Email, "person@example.test");
        var successSender = new SequenceSender(DeliveryOutcome.Succeeded(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake"));
        var permanentSender = new SequenceSender(DeliveryOutcome.Failed(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake", FailureKind.Permanent, "nope"));
        var retrySender = new SequenceSender(
            DeliveryOutcome.Deferred(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake"),
            DeliveryOutcome.Failed(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake", FailureKind.Transient, "try again"));

        // Act
        var success = await Dispatcher(successSender, maxAttempts: 1).DispatchAsync(envelope);
        var permanent = await Dispatcher(permanentSender, maxAttempts: 3).DispatchAsync(envelope);
        var exhausted = await Dispatcher(retrySender, maxAttempts: 2).DispatchAsync(envelope);
        Func<Task> invalid = () => Dispatcher(successSender, maxAttempts: 0).DispatchAsync(envelope);

        // Assert
        success.Status.Should().Be(DeliveryStatus.Succeeded);
        permanent.FailureKind.Should().Be(FailureKind.Permanent);
        exhausted.FailureKind.Should().Be(FailureKind.Transient);
        retrySender.Calls.Should().Be(2);
        await invalid.Should().ThrowAsync<InvalidOperationException>().WithMessage("*MaxAttempts*");
    }

    /// <summary>
    /// Verifies the sender resolver uses keyed, fallback, and missing-registration paths.
    /// </summary>
    [Fact]
    public void NotificationSenderResolver_UsesKeyedFallbackAndMissingPaths()
    {
        // Arrange
        var emailPayload = new EmailEnvelope("person@example.test", new EmailContent("Subject", "Body"))
        {
            From = "sender@example.test",
            FromDisplayName = "HoneyDrunk",
            Headers = new Dictionary<string, string> { ["X-Test"] = "true" },
        };
        var keyedSender = new SequenceSender(Success(Envelope(NotificationChannel.Email, "keyed@example.test")));
        var fallbackSender = new SequenceSender(Success(Envelope(NotificationChannel.Sms, "fallback@example.test")));
        var keyedServices = new ServiceCollection();
        keyedServices.AddKeyedSingleton<INotificationSender>(NotificationChannel.Email, keyedSender);
        using var keyedProvider = keyedServices.BuildServiceProvider();
        var fallbackServices = new ServiceCollection();
        fallbackServices.AddSingleton<INotificationSender>(fallbackSender);
        using var fallbackProvider = fallbackServices.BuildServiceProvider();
        using var emptyProvider = new ServiceCollection().BuildServiceProvider();

        // Act
        var keyed = new NotificationSenderResolver(keyedProvider).Resolve(NotificationChannel.Email);
        var fallback = new NotificationSenderResolver(fallbackProvider).Resolve(NotificationChannel.Sms);
        Action missing = () => new NotificationSenderResolver(emptyProvider).Resolve((NotificationChannel)42);

        // Assert
        emailPayload.From.Should().Be("sender@example.test");
        emailPayload.FromDisplayName.Should().Be("HoneyDrunk");
        emailPayload.Headers.Should().ContainKey("X-Test");
        keyed.Should().BeSameAs(keyedSender);
        fallback.Should().BeSameAs(fallbackSender);
        missing.Should().Throw<InvalidOperationException>().WithMessage("*42*");
    }
}
