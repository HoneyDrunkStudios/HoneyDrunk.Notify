namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents the outcome of a single delivery attempt by a provider.
/// </summary>
public enum DeliveryStatus
{
    /// <summary>
    /// The provider confirmed successful delivery.
    /// </summary>
    Succeeded = 0,

    /// <summary>
    /// The delivery attempt failed. See <see cref="FailureKind"/> for retry eligibility.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// The provider accepted the message but delivery is deferred (queued at the provider).
    /// </summary>
    Deferred = 2,
}
