namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// An optional hook that evaluates intake policy on a <see cref="NotificationRequest"/>
/// before it is accepted and enqueued.
/// </summary>
/// <remarks>
/// <para>
/// Implementations can enforce rate limits, honour opt-out lists, apply suppression rules,
/// or transform requests (e.g., override channel, inject tags). Multiple policies can be
/// registered; the gateway evaluates them in registration order and stops at the first denial.
/// </para>
/// <para>
/// This is an extension point — if no <see cref="INotificationPolicy"/> is registered,
/// all structurally valid requests are accepted.
/// </para>
/// </remarks>
public interface INotificationPolicy
{
    /// <summary>
    /// Evaluates the given request against this policy.
    /// </summary>
    /// <param name="request">The notification request to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="PolicyEvaluationResult"/> indicating allow, deny, or transform.</returns>
    Task<PolicyEvaluationResult> EvaluateAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
