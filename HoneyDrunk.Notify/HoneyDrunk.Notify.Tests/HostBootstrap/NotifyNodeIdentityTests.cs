using FluentAssertions;
using HoneyDrunk.Kernel.Abstractions;
using HoneyDrunk.Notify.HostBootstrap;
using Microsoft.Extensions.Configuration;

namespace HoneyDrunk.Notify.Tests.HostBootstrap;

/// <summary>
/// Tests Notify host identity resolution.
/// </summary>
public sealed class NotifyNodeIdentityTests
{
    /// <summary>
    /// Verifies the Kernel canonical Notify identity is used when deployment configuration is absent.
    /// </summary>
    [Fact]
    public void ResolveNodeId_UsesKernelCanonicalNotifyIdByDefault()
    {
        var configuration = BuildConfiguration([]);

        var nodeId = NotifyNodeIdentity.ResolveNodeId(configuration);

        nodeId.Should().Be(WellKnownNodes.Ops.Notify);
    }

    /// <summary>
    /// Verifies deployment-provided HONEYDRUNK_NODE_ID overrides the canonical fallback.
    /// </summary>
    [Fact]
    public void ResolveNodeId_PreservesHoneyDrunkNodeIdOverride()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["HONEYDRUNK_NODE_ID"] = "custom-notify",
            ["Grid:NodeId"] = "grid-notify",
        });

        var nodeId = NotifyNodeIdentity.ResolveNodeId(configuration);

        nodeId.Value.Should().Be("custom-notify");
    }

    /// <summary>
    /// Verifies Grid:NodeId is honored when the environment-style override is absent.
    /// </summary>
    [Fact]
    public void ResolveNodeId_UsesGridNodeIdWhenEnvOverrideIsAbsent()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Grid:NodeId"] = "grid-notify",
        });

        var nodeId = NotifyNodeIdentity.ResolveNodeId(configuration);

        nodeId.Value.Should().Be("grid-notify");
    }

    private static IConfiguration BuildConfiguration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
