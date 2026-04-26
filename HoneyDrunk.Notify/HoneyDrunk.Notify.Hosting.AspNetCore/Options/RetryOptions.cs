namespace HoneyDrunk.Notify.Hosting.AspNetCore.Options;

/// <summary>
/// Configuration for delivery retry behaviour.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before a notification is dead-lettered.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retry attempts. Actual delay may increase via exponential backoff.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the upper bound for the delay between retry attempts.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
}
