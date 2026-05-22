// <copyright file="CoverageGateBackfillTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

using AwesomeAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.HostBootstrap;
using HoneyDrunk.Notify.Hosting.AspNetCore.Health;
using HoneyDrunk.Notify.Hosting.AspNetCore.Options;
using HoneyDrunk.Notify.Hosting.AspNetCore.ServiceCollectionExtensions;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Queue.InMemory.DependencyInjection;
using HoneyDrunk.Notify.Worker.Composition;
using HoneyDrunk.Notify.Worker.Hosting;
using HoneyDrunk.Notify.Worker.Options;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.Tests.Telemetry;

/// <summary>
/// Focused coverage backfill for hosting behavior.
/// </summary>
public sealed partial class CoverageGateBackfillTests
{
    /// <summary>
    /// Verifies hosting and worker composition map options and register queue/provider services.
    /// </summary>
    [Fact]
    public void HostingAndWorkerComposition_RegisterExpectedRuntimeServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());

        // Act
        services.AddHoneyDrunkNotify(options =>
        {
            options.Enabled = false;
            options.Retry.MaxAttempts = 7;
            options.Retry.BaseDelay = TimeSpan.FromSeconds(1);
            options.Retry.MaxDelay = TimeSpan.FromSeconds(9);
            options.Policy.EnableDedupe = false;
            options.Policy.DedupeWindow = TimeSpan.FromSeconds(30);
            options.Templates.RootPath = "custom-templates";
            options.Templates.Extension = ".liquid";
            options.Templates.CacheEnabled = false;
            options.Templates.CacheTtl = TimeSpan.FromSeconds(5);
        });
        services.AddHoneyDrunkNotifyInMemoryQueue();
        services.AddHoneyDrunkNotifyWorker(options => options.QueueAdapter = "InMemory");
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<IOptions<NotifyRuntimeOptions>>().Value;
        var templates = provider.GetRequiredService<IOptions<HoneyDrunk.Notify.Options.TemplateOptions>>().Value;
        var worker = provider.GetRequiredService<IOptions<NotifyWorkerOptions>>().Value;

        // Assert
        runtime.Enabled.Should().BeFalse();
        runtime.MaxAttempts.Should().Be(7);
        runtime.EnableDedupe.Should().BeFalse();
        templates.RootPath.Should().Be("custom-templates");
        templates.Extension.Should().Be(".liquid");
        templates.CacheEnabled.Should().BeFalse();
        worker.QueueAdapter.Should().Be("InMemory");
        provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies host health, node identity, and fallback sender paths stay covered as runtime code.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HostRuntimeHelpers_ReportHealthIdentityAndNoOpFailures()
    {
        // Arrange
        var enabledContributor = new DefaultNotifyHealthContributor(
            Microsoft.Extensions.Options.Options.Create(new NotifyOptions { Enabled = true }));
        var disabledContributor = new DefaultNotifyHealthContributor(
            Microsoft.Extensions.Options.Options.Create(new NotifyOptions { Enabled = false }));
        var configuredNode = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Grid:NodeId"] = "notify-custom" })
            .Build();
        var environmentNode = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["HONEYDRUNK_NODE_ID"] = "notify-env", ["Grid:NodeId"] = "notify-grid" })
            .Build();
        var fallbackNode = new ConfigurationBuilder().Build();
        var sender = new NoOpNotificationSender(NullLogger<NoOpNotificationSender>.Instance);
        var envelope = Envelope(NotificationChannel.Email, "person@example.test");

        // Act
        var healthy = await enabledContributor.CheckAsync();
        var unhealthy = await disabledContributor.CheckAsync();
        var configured = NotifyNodeIdentity.ResolveNodeId(configuredNode);
        var environment = NotifyNodeIdentity.ResolveNodeId(environmentNode);
        var fallback = NotifyNodeIdentity.ResolveNodeId(fallbackNode);
        var outcome = await sender.SendAsync(envelope);
        Func<Task> nullEnvelope = () => sender.SendAsync(null!);

        // Assert
        healthy.Status.Should().Be(NotifyHealthStatus.Healthy);
        unhealthy.Status.Should().Be(NotifyHealthStatus.Unhealthy);
        configured.Value.Should().Be("notify-custom");
        environment.Value.Should().Be("notify-env");
        fallback.Value.Should().Be("honeydrunk-notify");
        outcome.NotificationId.Should().Be(envelope.NotificationId);
        outcome.Provider.Should().Be("noop");
        outcome.Status.Should().Be(DeliveryStatus.Failed);
        outcome.FailureKind.Should().Be(FailureKind.Permanent);
        await nullEnvelope.Should().ThrowAsync<ArgumentNullException>();
    }
}
