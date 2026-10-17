using HoneyDrunk.Notify.Hosting.AspNetCore.Health;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace HoneyDrunk.Notify.Functions;

/// <summary>
/// Health endpoint for Notify.Functions, exposed at <c>/api/health</c>. Used by the
/// Container Apps / Function App deploy workflow as the post-deploy readiness probe.
/// Aggregates every <see cref="INotifyHealthContributor"/> via the shared
/// <see cref="NotifyHealthEvaluator"/> — the same logic Notify.Worker serves on
/// <c>/health/ready</c> — and returns 503 when the subsystem is
/// <see cref="NotifyHealthStatus.Unhealthy"/>.
/// </summary>
public sealed class HealthFunction(NotifyHealthEvaluator evaluator)
{
    private readonly NotifyHealthEvaluator _evaluator =
        evaluator ?? throw new ArgumentNullException(nameof(evaluator));

    /// <summary>
    /// Handles health probe requests.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>200 when healthy or degraded; 503 when unhealthy.</returns>
    [Function(nameof(HealthFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var report = await _evaluator.EvaluateAsync(cancellationToken);

        var statusCode = report.Status == NotifyHealthStatus.Unhealthy
            ? HttpStatusCode.ServiceUnavailable
            : HttpStatusCode.OK;

        // Mirrors VaultInvalidationFunction: create with the final status and
        // write the body with WriteStringAsync, which (unlike WriteAsJsonAsync)
        // does not reset StatusCode — so no post-write status mutation.
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(new { status = report.Status.ToString(), message = report.Message }),
            cancellationToken);

        return response;
    }
}
