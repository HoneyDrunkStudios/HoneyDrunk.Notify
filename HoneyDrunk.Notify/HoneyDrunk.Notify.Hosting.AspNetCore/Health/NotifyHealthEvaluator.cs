namespace HoneyDrunk.Notify.Hosting.AspNetCore.Health;

/// <summary>
/// Aggregates every registered <see cref="INotifyHealthContributor"/> into a single
/// <see cref="NotifyHealthReport"/>, taking the most severe observed status.
/// Shared by the Notify.Worker health endpoints and the Notify.Functions health
/// endpoint so readiness is evaluated identically across both deployables.
/// </summary>
/// <param name="contributors">All registered notification health contributors.</param>
public sealed class NotifyHealthEvaluator(IEnumerable<INotifyHealthContributor> contributors)
{
    private readonly IReadOnlyList<INotifyHealthContributor> _contributors = contributors.ToList();

    /// <summary>
    /// Evaluates every contributor and returns the aggregate report. The aggregate
    /// status is the most severe individual status
    /// (<see cref="NotifyHealthStatus.Unhealthy"/> &gt;
    /// <see cref="NotifyHealthStatus.Degraded"/> &gt;
    /// <see cref="NotifyHealthStatus.Healthy"/>). When no contributors are registered
    /// the subsystem is reported <see cref="NotifyHealthStatus.Healthy"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the evaluation.</param>
    /// <returns>The aggregate <see cref="NotifyHealthReport"/>.</returns>
    public async Task<NotifyHealthReport> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        if (_contributors.Count == 0)
        {
            return new NotifyHealthReport(
                NotifyHealthStatus.Healthy,
                "No health contributors registered.");
        }

        var worst = NotifyHealthStatus.Healthy;
        var messages = new List<string>(_contributors.Count);

        foreach (var contributor in _contributors)
        {
            var report = await contributor.CheckAsync(cancellationToken);

            if (report.Status > worst)
            {
                worst = report.Status;
            }

            messages.Add(report.Message);
        }

        return new NotifyHealthReport(worst, string.Join("; ", messages));
    }
}
