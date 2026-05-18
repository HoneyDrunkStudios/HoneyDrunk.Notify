using HoneyDrunk.Kernel.Abstractions;
using HoneyDrunk.Kernel.Abstractions.Identity;

namespace HoneyDrunk.Notify.HostBootstrap;

/// <summary>
/// Resolves Notify host identity from deployment configuration with Kernel canonical fallback.
/// </summary>
internal static class NotifyNodeIdentity
{
    /// <summary>
    /// Resolves the Notify node identifier without overwriting deployment-provided configuration.
    /// </summary>
    /// <param name="configuration">The host configuration.</param>
    /// <returns>The resolved node identifier.</returns>
    public static NodeId ResolveNodeId(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredNodeId = new[]
            {
                configuration["HONEYDRUNK_NODE_ID"],
                configuration["Grid:NodeId"],
            }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return string.IsNullOrWhiteSpace(configuredNodeId)
            ? WellKnownNodes.Ops.Notify
            : new NodeId(configuredNodeId);
    }
}
