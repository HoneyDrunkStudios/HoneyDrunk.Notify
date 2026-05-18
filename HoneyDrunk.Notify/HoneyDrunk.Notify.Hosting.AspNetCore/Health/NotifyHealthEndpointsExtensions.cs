using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HoneyDrunk.Notify.Hosting.AspNetCore.Health;

/// <summary>
/// Maps the Notify health endpoints onto an ASP.NET Core host (Notify.Worker).
/// </summary>
public static class NotifyHealthEndpointsExtensions
{
    /// <summary>
    /// Maps <c>/health</c>, <c>/health/live</c>, and <c>/health/ready</c>.
    /// <para>
    /// <c>/health</c> and <c>/health/live</c> are liveness probes: a 200 response
    /// confirms the process started, DI was built, and Vault bootstrap succeeded.
    /// They are dependency-free so the container-app deploy traffic gate is not
    /// tripped by transient downstream blips.
    /// </para>
    /// <para>
    /// <c>/health/ready</c> aggregates every <see cref="INotifyHealthContributor"/>
    /// via <see cref="NotifyHealthEvaluator"/> and returns 503 when the subsystem
    /// is <see cref="NotifyHealthStatus.Unhealthy"/>.
    /// </para>
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same <paramref name="endpoints"/> for chaining.</returns>
    public static IEndpointRouteBuilder MapNotifyHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
            .WithName("NotifyHealth")
            .WithTags("Health");

        endpoints.MapGet("/health/live", () => Results.Ok(new { status = "Live" }))
            .WithName("NotifyLiveness")
            .WithTags("Health");

        endpoints.MapGet(
            "/health/ready",
            async (NotifyHealthEvaluator evaluator, CancellationToken cancellationToken) =>
            {
                var report = await evaluator.EvaluateAsync(cancellationToken);
                var payload = new { status = report.Status.ToString(), message = report.Message };

                return report.Status == NotifyHealthStatus.Unhealthy
                    ? Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable)
                    : Results.Ok(payload);
            })
            .WithName("NotifyReadiness")
            .WithTags("Health");

        return endpoints;
    }
}
