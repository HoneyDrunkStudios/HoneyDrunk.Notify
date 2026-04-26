using HoneyDrunk.Notify.Abstractions;

namespace HoneyDrunk.Notify.Policies;

/// <summary>
/// Default policy that permits all notification requests.
/// </summary>
#pragma warning disable CA1812
internal sealed class AllowAllPolicy : INotificationPolicy
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public Task<PolicyEvaluationResult> EvaluateAsync(
        NotificationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(PolicyEvaluationResult.Allow());
}
