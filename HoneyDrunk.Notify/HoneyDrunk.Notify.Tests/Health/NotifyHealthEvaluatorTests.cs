using FluentAssertions;
using HoneyDrunk.Notify.Hosting.AspNetCore.Health;

namespace HoneyDrunk.Notify.Tests.Health;

/// <summary>
/// Tests for <see cref="NotifyHealthEvaluator"/> aggregation behavior.
/// </summary>
public sealed class NotifyHealthEvaluatorTests
{
    /// <summary>
    /// With no contributors the subsystem is reported healthy.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task EvaluateAsync_NoContributors_ReportsHealthy()
    {
        var evaluator = new NotifyHealthEvaluator([]);

        var report = await evaluator.EvaluateAsync();

        report.Status.Should().Be(NotifyHealthStatus.Healthy);
        report.Message.Should().Be("No health contributors registered.");
    }

    /// <summary>
    /// When every contributor is healthy the aggregate is healthy.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task EvaluateAsync_AllHealthy_ReportsHealthy()
    {
        var evaluator = new NotifyHealthEvaluator(
        [
            new StubContributor(NotifyHealthStatus.Healthy, "a ok"),
            new StubContributor(NotifyHealthStatus.Healthy, "b ok"),
        ]);

        var report = await evaluator.EvaluateAsync();

        report.Status.Should().Be(NotifyHealthStatus.Healthy);
        report.Message.Should().Be("a ok; b ok");
    }

    /// <summary>
    /// A single degraded contributor degrades the aggregate.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task EvaluateAsync_OneDegraded_ReportsDegraded()
    {
        var evaluator = new NotifyHealthEvaluator(
        [
            new StubContributor(NotifyHealthStatus.Healthy, "ok"),
            new StubContributor(NotifyHealthStatus.Degraded, "slow"),
        ]);

        var report = await evaluator.EvaluateAsync();

        report.Status.Should().Be(NotifyHealthStatus.Degraded);
    }

    /// <summary>
    /// A single unhealthy contributor makes the aggregate unhealthy regardless
    /// of the other contributors' status.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task EvaluateAsync_OneUnhealthy_ReportsUnhealthyEvenWhenOthersHealthy()
    {
        var evaluator = new NotifyHealthEvaluator(
        [
            new StubContributor(NotifyHealthStatus.Healthy, "ok"),
            new StubContributor(NotifyHealthStatus.Unhealthy, "down"),
            new StubContributor(NotifyHealthStatus.Degraded, "slow"),
        ]);

        var report = await evaluator.EvaluateAsync();

        report.Status.Should().Be(NotifyHealthStatus.Unhealthy);
        report.Message.Should().Contain("down");
    }

    /// <summary>
    /// Test contributor that returns a fixed report.
    /// </summary>
    private sealed class StubContributor(NotifyHealthStatus status, string message) : INotifyHealthContributor
    {
        /// <inheritdoc />
        public Task<NotifyHealthReport> CheckAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new NotifyHealthReport(status, message));
    }
}
