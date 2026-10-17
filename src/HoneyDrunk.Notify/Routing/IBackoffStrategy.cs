namespace HoneyDrunk.Notify.Routing;

/// <summary>
/// Computes the delay between retry attempts.
/// Extracted as an abstraction so dispatch logic is unit-testable without real sleeps.
/// </summary>
public interface IBackoffStrategy
{
    /// <summary>
    /// Calculates the delay before the next retry attempt.
    /// </summary>
    /// <param name="attempt">The zero-based attempt index (0 = first retry after initial failure).</param>
    /// <param name="baseDelay">The configured base delay.</param>
    /// <param name="maxDelay">The upper bound for backoff.</param>
    /// <returns>The computed delay duration.</returns>
    TimeSpan Calculate(int attempt, TimeSpan baseDelay, TimeSpan maxDelay);
}
