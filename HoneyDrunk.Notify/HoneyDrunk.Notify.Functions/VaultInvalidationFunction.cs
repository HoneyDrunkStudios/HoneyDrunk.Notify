using HoneyDrunk.Vault.EventGrid.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace HoneyDrunk.Notify.Functions;

/// <summary>
/// HTTP endpoint used by Event Grid to invalidate Vault cache entries after secret rotation.
/// </summary>
public sealed class VaultInvalidationFunction(VaultInvalidationFunctionHandler handler)
{
    private readonly VaultInvalidationFunctionHandler _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    /// <summary>
    /// Handles Event Grid cache invalidation webhook calls.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response.</returns>
    [Function(nameof(VaultInvalidationFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/vault/invalidate")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var headers = request.Headers.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.FirstOrDefault(),
            StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var result = await _handler.HandleAsync(headers, body, cancellationToken);
        var response = request.CreateResponse((HttpStatusCode)result.StatusCode);

        if (!string.IsNullOrWhiteSpace(result.Body))
        {
            response.Headers.Add("Content-Type", result.ContentType);
            await response.WriteStringAsync(result.Body, cancellationToken);
        }

        return response;
    }
}
