using FluentAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Sms;
using HoneyDrunk.Notify.DependencyInjection;
using HoneyDrunk.Notify.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Notify.IntegrationTests;

/// <summary>
/// Verifies the multi-channel sender resolver routes notifications to the correct
/// channel-specific <see cref="INotificationSender"/> via keyed DI registrations.
/// </summary>
public sealed class ChannelRoutingTests
{
    /// <summary>
    /// Verifies that email notifications are routed to the email sender.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Resolver_RoutesEmail_ToEmailSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkNotifyRuntime(o =>
        {
            o.MaxAttempts = 1;
            o.EnableDedupe = false;
        });

        var emailSender = new FakeChannelSender("email-provider");
        services.AddKeyedSingleton<INotificationSender>(NotificationChannel.Email, emailSender);

        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<NotificationDispatcher>();

        var envelope = new NotificationEnvelope(
            NotificationId.NewId(),
            NotificationChannel.Email,
            new Recipient(NotificationChannel.Email, "user@example.com"),
            new TemplateKey("welcome"),
            new Dictionary<string, object?> { ["name"] = "Test" });

        var outcome = await dispatcher.DispatchAsync(envelope);

        outcome.Provider.Should().Be("email-provider");
        outcome.Status.Should().Be(DeliveryStatus.Succeeded);
        emailSender.CallCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that SMS notifications are routed to the SMS sender.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Resolver_RoutesSms_ToSmsSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkNotifyRuntime(o =>
        {
            o.MaxAttempts = 1;
            o.EnableDedupe = false;
        });

        var smsSender = new FakeChannelSender("sms-provider");
        services.AddKeyedSingleton<INotificationSender>(NotificationChannel.Sms, smsSender);

        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<NotificationDispatcher>();

        var smsPayload = new SmsEnvelope("+15551234567", "Hello from Notify!");
        var envelope = new NotificationEnvelope(
            NotificationId.NewId(),
            NotificationChannel.Sms,
            new Recipient(NotificationChannel.Sms, "+15551234567"),
            new TemplateKey("sms.welcome"),
            new Dictionary<string, object?> { ["name"] = "Test" })
        {
            Payload = smsPayload,
        };

        var outcome = await dispatcher.DispatchAsync(envelope);

        outcome.Provider.Should().Be("sms-provider");
        outcome.Status.Should().Be(DeliveryStatus.Succeeded);
        smsSender.CallCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that multiple channels are routed independently.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Resolver_RoutesMultipleChannels_Independently()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkNotifyRuntime(o =>
        {
            o.MaxAttempts = 1;
            o.EnableDedupe = false;
        });

        var emailSender = new FakeChannelSender("email-provider");
        var smsSender = new FakeChannelSender("sms-provider");
        services.AddKeyedSingleton<INotificationSender>(NotificationChannel.Email, emailSender);
        services.AddKeyedSingleton<INotificationSender>(NotificationChannel.Sms, smsSender);

        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<NotificationDispatcher>();

        var emailEnvelope = new NotificationEnvelope(
            NotificationId.NewId(),
            NotificationChannel.Email,
            new Recipient(NotificationChannel.Email, "user@example.com"),
            new TemplateKey("email.welcome"),
            new Dictionary<string, object?> { ["name"] = "Test" });

        var smsEnvelope = new NotificationEnvelope(
            NotificationId.NewId(),
            NotificationChannel.Sms,
            new Recipient(NotificationChannel.Sms, "+15551234567"),
            new TemplateKey("sms.welcome"),
            new Dictionary<string, object?> { ["name"] = "Test" })
        {
            Payload = new SmsEnvelope("+15551234567", "Hi!"),
        };

        var emailOutcome = await dispatcher.DispatchAsync(emailEnvelope);
        var smsOutcome = await dispatcher.DispatchAsync(smsEnvelope);

        emailOutcome.Provider.Should().Be("email-provider");
        smsOutcome.Provider.Should().Be("sms-provider");
        emailSender.CallCount.Should().Be(1);
        smsSender.CallCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that resolving an unregistered channel throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Resolver_ThrowsInvalidOperation_WhenNoSenderRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkNotifyRuntime(o =>
        {
            o.MaxAttempts = 1;
            o.EnableDedupe = false;
        });

        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<INotificationSenderResolver>();

        var act = () => resolver.Resolve(NotificationChannel.Sms);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No INotificationSender registered*Sms*");
    }

    private sealed class FakeChannelSender(string providerName) : INotificationSender
    {
        public int CallCount { get; private set; }

        public Task<DeliveryOutcome> SendAsync(NotificationEnvelope envelope, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(DeliveryOutcome.Succeeded(
                envelope.NotificationId,
                AttemptId.NewId(),
                envelope.Channel,
                providerName));
        }
    }
}
