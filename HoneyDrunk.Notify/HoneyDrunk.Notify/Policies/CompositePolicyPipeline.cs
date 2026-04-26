using HoneyDrunk.Notify.Abstractions;

namespace HoneyDrunk.Notify.Policies;

/// <summary>
/// Evaluates multiple <see cref="INotificationPolicy"/> instances in registration order.
/// Stops at the first denial; applies the last transform if multiple policies transform the request.
/// </summary>
#pragma warning disable CA1812
internal sealed class CompositePolicyPipeline(IEnumerable<INotificationPolicy> policies) : INotificationPolicy
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public async Task<PolicyEvaluationResult> EvaluateAsync(
        NotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var effectiveRequest = request;

        foreach (var policyInstance in policies)
        {
            var result = await policyInstance.EvaluateAsync(effectiveRequest, cancellationToken);

            if (!result.IsAllowed)
                return result;

            if (result.TransformedRequest is not null)
                effectiveRequest = result.TransformedRequest;
        }

        return effectiveRequest == request
            ? PolicyEvaluationResult.Allow()
            : PolicyEvaluationResult.AllowWithTransform(effectiveRequest);
    }
}
