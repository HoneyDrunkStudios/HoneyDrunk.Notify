// <copyright file="CoverageGateBackfillTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

using AwesomeAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.Abstractions.Models.Sms;
using HoneyDrunk.Notify.DependencyInjection;
using HoneyDrunk.Notify.Providers.Email.Resend.DependencyInjection;
using HoneyDrunk.Notify.Providers.Email.Smtp.DependencyInjection;
using HoneyDrunk.Notify.Providers.Sms.Twilio.DependencyInjection;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Queue.AzureStorage.DependencyInjection;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Notify.Tests.Telemetry;

/// <summary>
/// Focused coverage backfill for provider behavior.
/// </summary>
public sealed partial class CoverageGateBackfillTests
{
    /// <summary>
    /// Verifies provider registration resolves keyed senders and safe guard paths without external service calls.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ProviderRegistrations_ResolveSendersAndRejectInvalidPayloadsWithoutExternalCalls()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());
        services.AddHoneyDrunkNotifyRuntime();
        services.AddHoneyDrunkNotifySmtpProvider(options => options.FromAddress = "smtp@example.test");
        services.AddHoneyDrunkNotifyTwilioProvider(options => options.FromNumber = "+15551234567");
        using var provider = services.BuildServiceProvider();
        var emailEnvelope = Envelope(NotificationChannel.Email, "person@example.test") with { Payload = new SmsEnvelope("+15550000000", "wrong") };
        var smsEnvelope = Envelope(NotificationChannel.Sms, "+15550000000") with { Payload = new EmailEnvelope("person@example.test", new EmailContent("s", "b")) };

        // Act
        var emailSender = provider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Email);
        var smsSender = provider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Sms);
        var emailResult = await emailSender.SendAsync(emailEnvelope);
        var smsResult = await smsSender.SendAsync(smsEnvelope);

        // Assert
        emailResult.Status.Should().Be(DeliveryStatus.Failed);
        emailResult.FailureKind.Should().Be(FailureKind.Permanent);
        smsResult.Status.Should().Be(DeliveryStatus.Failed);
        smsResult.FailureKind.Should().Be(FailureKind.Permanent);
        provider.GetRequiredService<INotificationSender>().Should().BeSameAs(emailSender);
    }

    /// <summary>
    /// Verifies each provider fails closed when the payload is valid but no sender address is configured.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ProviderSenders_RejectMissingSenderConfigurationBeforeExternalCalls()
    {
        // Arrange
        using var smtpProvider = BuildProvider(services => services.AddHoneyDrunkNotifySmtpProvider());
        using var resendProvider = BuildProvider(services => services.AddHoneyDrunkNotifyResendProvider(_ => { }));
        using var twilioProvider = BuildProvider(services => services.AddHoneyDrunkNotifyTwilioProvider(_ => { }));
        var smtpSender = smtpProvider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Email);
        var resendSender = resendProvider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Email);
        var twilioSender = twilioProvider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Sms);
        var email = Envelope(NotificationChannel.Email, "person@example.test") with
        {
            Payload = new EmailEnvelope("person@example.test", new EmailContent("Subject", "Body")),
        };
        var sms = Envelope(NotificationChannel.Sms, "+15550000000") with
        {
            Payload = new SmsEnvelope("+15550000000", "Hello"),
        };

        // Act
        var smtp = await smtpSender.SendAsync(email);
        var resend = await resendSender.SendAsync(email);
        var twilio = await twilioSender.SendAsync(sms);

        // Assert
        smtp.Provider.Should().Be("smtp");
        smtp.Status.Should().Be(DeliveryStatus.Failed);
        smtp.ErrorMessage.Should().Contain("No sender address configured");
        resend.Provider.Should().Be("resend");
        resend.Status.Should().Be(DeliveryStatus.Failed);
        resend.ErrorMessage.Should().Contain("No sender address configured");
        twilio.Provider.Should().Be("twilio");
        twilio.Status.Should().Be(DeliveryStatus.Failed);
        twilio.ErrorMessage.Should().Contain("No sender phone number configured");
    }

    /// <summary>
    /// Verifies Azure Storage queue registration exposes both queue and dead-letter abstractions.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AzureStorageQueueRegistration_ExposesQueueAndDeadLetterInspector()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());

        // Act
        services.AddHoneyDrunkNotifyAzureStorageQueue(options =>
        {
            options.ConnectionString = "UseDevelopmentStorage=true";
            options.QueueName = "notify";
        });
        await using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<INotificationQueue>().Should().NotBeNull();
        provider.GetRequiredService<IDeadLetterInspector>().Should().NotBeNull();
    }
}
