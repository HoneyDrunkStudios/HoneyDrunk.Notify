namespace HoneyDrunk.Notify.Routing;

/// <summary>
/// Exponential backoff with a configurable cap.
/// Delay = min(baseDelay * 2^attempt, maxDelay).
/// </summary>
#pragma warning disable CA1812
internal sealed class ExponentialBackoffStrategy : IBackoffStrategy
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public TimeSpan Calculate(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        var delayMs = baseDelay.TotalMilliseconds * Math.Pow(2, attempt);
        var capped = Math.Min(delayMs, maxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capped);
    }
}
