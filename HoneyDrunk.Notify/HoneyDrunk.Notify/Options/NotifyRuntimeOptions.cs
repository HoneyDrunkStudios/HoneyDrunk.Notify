namespace HoneyDrunk.Notify.Options;

/// <summary>
/// Core runtime options for the notification pipeline.
/// These are provider-agnostic settings that control gateway behavior, retry, and deduplication.
/// </summary>
public sealed class NotifyRuntimeOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the notification pipeline is active.
    /// When disabled, the gateway rejects all requests.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts per notification.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay for exponential backoff between retries.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the upper bound for backoff delay.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether idempotency-based deduplication is enabled.
    /// </summary>
    public bool EnableDedupe { get; set; } = true;

    /// <summary>
    /// Gets or sets the time window during which duplicate idempotency keys are rejected.
    /// </summary>
    public TimeSpan DedupeWindow { get; set; } = TimeSpan.FromMinutes(10);
}
