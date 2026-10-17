using AwesomeAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Notify.IntegrationTests;

/// <summary>
/// Verifies that Notify intake accepts structurally valid requests without owning
/// preference, cadence, or suppression policy decisions.
/// </summary>
public sealed class NotificationGatewayIntakeTests
{
    /// <summary>
    /// Verifies that a structurally valid request is accepted directly by Notify intake.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task EnqueueAsync_accepts_valid_request_without_policy_pipeline()
    {
        await using var provider = CreateProvider();
        var gateway = provider.GetRequiredService<INotificationGateway>();

        var outcome = await gateway.EnqueueAsync(CreateSmsRequest());

        outcome.Status.Should().Be(NotificationAcceptanceStatus.Accepted);
        outcome.RejectionReason.Should().Be(RejectionReason.None);
    }

    /// <summary>
    /// Verifies that runtime-disabled intake uses an operational rejection reason,
    /// not a Communications-owned policy decision.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task EnqueueAsync_when_runtime_disabled_rejects_as_runtime_disabled()
    {
        await using var provider = CreateProvider(enabled: false);
        var gateway = provider.GetRequiredService<INotificationGateway>();

        var outcome = await gateway.EnqueueAsync(CreateSmsRequest());

        outcome.Status.Should().Be(NotificationAcceptanceStatus.Rejected);
        outcome.RejectionReason.Should().Be(RejectionReason.RuntimeDisabled);
    }

    private static ServiceProvider CreateProvider(bool enabled = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHoneyDrunkNotifyRuntime(options => options.Enabled = enabled);

        return services.BuildServiceProvider();
    }

    private static NotificationRequest CreateSmsRequest() =>
        new(
            NotificationChannel.Sms,
            new Recipient(NotificationChannel.Sms, "+15555550100"),
            new TemplateKey("notify-intake-canary"),
            new Dictionary<string, object?> { ["Name"] = "Notify" });
}
