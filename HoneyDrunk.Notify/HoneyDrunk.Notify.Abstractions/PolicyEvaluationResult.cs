namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents the result of a policy evaluation on a <see cref="NotificationRequest"/>.
/// </summary>
/// <remarks>
/// A policy can allow, deny, or transform the request before it enters the delivery pipeline.
/// </remarks>
/// <param name="IsAllowed">Whether the request is permitted to proceed.</param>
public sealed record PolicyEvaluationResult(bool IsAllowed)
{
    /// <summary>
    /// Gets the rejection reason when the request is denied.
    /// <see cref="RejectionReason.None"/> when <see cref="IsAllowed"/> is <c>true</c>.
    /// </summary>
    public RejectionReason RejectionReason { get; init; } = RejectionReason.None;

    /// <summary>
    /// Gets an optional detail message explaining the policy decision.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Gets an optional transformed request. When non-null, the gateway should use this
    /// request instead of the original (e.g., channel override, tag injection).
    /// </summary>
    public NotificationRequest? TransformedRequest { get; init; }

    /// <summary>
    /// Creates an "allowed" policy result with no modifications.
    /// </summary>
    /// <returns>A permissive <see cref="PolicyEvaluationResult"/>.</returns>
    public static PolicyEvaluationResult Allow() => new(true);

    /// <summary>
    /// Creates a "denied" policy result.
    /// </summary>
    /// <param name="reason">The reason for denial.</param>
    /// <param name="detail">Optional human-readable detail.</param>
    /// <returns>A denied <see cref="PolicyEvaluationResult"/>.</returns>
    public static PolicyEvaluationResult Deny(RejectionReason reason, string? detail = null) =>
        new(false) { RejectionReason = reason, Detail = detail };

    /// <summary>
    /// Creates an "allowed with transformation" policy result.
    /// </summary>
    /// <param name="transformedRequest">The modified request to use instead of the original.</param>
    /// <returns>A permissive <see cref="PolicyEvaluationResult"/> carrying a transformed request.</returns>
    public static PolicyEvaluationResult AllowWithTransform(NotificationRequest transformedRequest) =>
        new(true) { TransformedRequest = transformedRequest };
}
