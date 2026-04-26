using HoneyDrunk.Notify.Hosting.AspNetCore.Options;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.Hosting.AspNetCore.Health;

/// <summary>
/// Default health contributor that reports <see cref="NotifyHealthStatus.Healthy"/> when
/// the notification subsystem is enabled, and <see cref="NotifyHealthStatus.Unhealthy"/> when disabled.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class DefaultNotifyHealthContributor(IOptions<NotifyOptions> options) : INotifyHealthContributor
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public Task<NotifyHealthReport> CheckAsync(CancellationToken cancellationToken = default)
    {
        var report = options.Value.Enabled
            ? new NotifyHealthReport(NotifyHealthStatus.Healthy, "Notification subsystem is enabled.")
            : new NotifyHealthReport(NotifyHealthStatus.Unhealthy, "Notification subsystem is disabled.");

        return Task.FromResult(report);
    }
}
