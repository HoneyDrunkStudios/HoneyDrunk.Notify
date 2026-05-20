// <copyright file="CoverageGateBackfillTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

using FluentAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Queue.Abstractions;

namespace HoneyDrunk.Notify.Tests.Telemetry;

/// <summary>
/// Focused coverage backfill for contract behavior.
/// </summary>
public sealed partial class CoverageGateBackfillTests
{
    /// <summary>
    /// Verifies strongly typed ULID identifiers round-trip valid values and reject invalid text.
    /// </summary>
    [Fact]
    public void StronglyTypedIdentifiers_RoundTripAndRejectInvalidText()
    {
        // Arrange
        var ulid = Ulid.NewUlid();

        // Act
        var notificationId = new NotificationId(ulid);
        var attemptId = new AttemptId(ulid.ToString());
        var parsedNotification = NotificationId.TryParse(ulid.ToString(), out var parsedNotificationId);
        var parsedAttempt = AttemptId.TryParse(ulid.ToString(), out var parsedAttemptId);
        var failedNotification = NotificationId.TryParse("not-a-ulid", out var failedNotificationId);
        var failedAttempt = AttemptId.TryParse("not-a-ulid", out var failedAttemptId);

        // Assert
        ((string)notificationId).Should().Be(ulid.ToString());
        notificationId.ToUlid().Should().Be(ulid);
        NotificationId.FromUlid(ulid).Should().Be(notificationId);
        parsedNotification.Should().BeTrue();
        parsedNotificationId.Should().Be(notificationId);
        failedNotification.Should().BeFalse();
        failedNotificationId.Should().Be(default(NotificationId));
        ((Ulid)attemptId).Should().Be(ulid);
        attemptId.ToUlid().Should().Be(ulid);
        AttemptId.FromUlid(ulid).Should().Be(attemptId);
        parsedAttempt.Should().BeTrue();
        parsedAttemptId.Should().Be(attemptId);
        failedAttempt.Should().BeFalse();
        failedAttemptId.Should().Be(default(AttemptId));
        Action invalidNotification = () => _ = new NotificationId("bad");
        Action invalidAttempt = () => _ = new AttemptId("bad");
        invalidNotification.Should().Throw<ArgumentException>();
        invalidAttempt.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies simple contract factories preserve status, failure, and provider details.
    /// </summary>
    [Fact]
    public void OutcomeFactories_PreserveProviderStatusAndFailureDetails()
    {
        // Arrange
        var notificationId = NotificationId.NewId();
        var attemptId = AttemptId.NewId();
        var acceptedAt = DateTimeOffset.Parse("2026-05-19T16:00:00Z", null, System.Globalization.DateTimeStyles.AssumeUniversal);

        // Act
        var succeeded = DeliveryOutcome.Succeeded(notificationId, attemptId, NotificationChannel.Email, "smtp", "provider-1");
        var failed = DeliveryOutcome.Failed(notificationId, attemptId, NotificationChannel.Sms, "twilio", FailureKind.Policy, "blocked");
        var deferred = DeliveryOutcome.Deferred(notificationId, attemptId, NotificationChannel.Email, "resend", "provider-2");
        var accepted = NotificationOutcome.Accepted(notificationId, acceptedAt);
        var rejected = NotificationOutcome.Rejected(notificationId, acceptedAt, RejectionReason.ValidationFailed, "bad recipient");

        // Assert
        succeeded.Status.Should().Be(DeliveryStatus.Succeeded);
        succeeded.FailureKind.Should().Be(FailureKind.None);
        succeeded.ProviderMessageId.Should().Be("provider-1");
        failed.Status.Should().Be(DeliveryStatus.Failed);
        failed.FailureKind.Should().Be(FailureKind.Policy);
        failed.ErrorMessage.Should().Be("blocked");
        deferred.Status.Should().Be(DeliveryStatus.Deferred);
        deferred.ProviderMessageId.Should().Be("provider-2");
        accepted.Status.Should().Be(NotificationAcceptanceStatus.Accepted);
        accepted.RejectionReason.Should().Be(RejectionReason.None);
        rejected.Status.Should().Be(NotificationAcceptanceStatus.Rejected);
        rejected.RejectionReason.Should().Be(RejectionReason.ValidationFailed);
        rejected.RejectionDetail.Should().Be("bad recipient");
    }

    /// <summary>
    /// Verifies value objects validate required address and idempotency constraints.
    /// </summary>
    [Fact]
    public void ValueObjects_ValidateRecipientAndIdempotencyConstraints()
    {
        // Act
        var recipient = Recipient.Email("person@example.test");
        var key = new IdempotencyKey("order-123");
        Action missingRecipient = () => Recipient.Email(" ");
        Action missingKey = () => _ = new IdempotencyKey(" ");
        Action longKey = () => _ = new IdempotencyKey(new string('x', 257));

        // Assert
        recipient.Channel.Should().Be(NotificationChannel.Email);
        recipient.Address.Should().Be("person@example.test");
        ((string)key).Should().Be("order-123");
        key.ToString().Should().Be("order-123");
        missingRecipient.Should().Throw<ArgumentException>();
        missingKey.Should().Throw<ArgumentException>();
        longKey.Should().Throw<ArgumentException>().WithMessage("*256*");
    }

    /// <summary>
    /// Verifies provider and queue options expose default values and retain configured values.
    /// </summary>
    [Fact]
    public void ProviderAndQueueOptions_ExposeDefaultsAndConfiguredValues()
    {
        // Arrange / Act
        var smtp = new HoneyDrunk.Notify.Providers.Email.Smtp.SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 2525,
            UseSsl = true,
            FromAddress = "from@example.test",
            FromDisplayName = "HoneyDrunk",
        };
        var resend = new HoneyDrunk.Notify.Providers.Email.Resend.ResendOptions
        {
            FromAddress = "resend@example.test",
            FromDisplayName = "Resend",
        };
        var twilio = new HoneyDrunk.Notify.Providers.Sms.Twilio.TwilioOptions
        {
            FromNumber = "+15551234567",
        };
        var queue = new HoneyDrunk.Notify.Queue.AzureStorage.AzureStorageQueueOptions
        {
            QueueName = "notify-main",
            DeadLetterQueueName = "notify-dlq",
            ConnectionStringSecretName = "QueueSecret",
            CreateIfNotExists = false,
            MaxBatchSize = 16,
            MaxDeliveryAttempts = 9,
        };
        var notificationQueue = new NotificationQueueOptions
        {
            QueueName = "notify",
            DeadLetterQueueName = null,
        };

        // Assert
        smtp.Host.Should().Be("smtp.example.test");
        smtp.Port.Should().Be(2525);
        smtp.UseSsl.Should().BeTrue();
        smtp.FromAddress.Should().Be("from@example.test");
        smtp.FromDisplayName.Should().Be("HoneyDrunk");
        resend.FromAddress.Should().Be("resend@example.test");
        resend.FromDisplayName.Should().Be("Resend");
        twilio.FromNumber.Should().Be("+15551234567");
        queue.QueueName.Should().Be("notify-main");
        queue.EffectiveDeadLetterQueueName.Should().Be("notify-dlq");
        queue.ConnectionStringSecretName.Should().Be("QueueSecret");
        queue.CreateIfNotExists.Should().BeFalse();
        queue.MaxBatchSize.Should().Be(16);
        queue.MaxDeliveryAttempts.Should().Be(9);
        notificationQueue.EffectiveDeadLetterQueueName.Should().Be("notify-dlq");
    }
}
