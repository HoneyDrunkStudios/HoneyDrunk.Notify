namespace HoneyDrunk.Notify.Hosting.AspNetCore.Options;

/// <summary>
/// Configuration for notification intake policies (deduplication, suppression, etc.).
/// </summary>
public sealed class PolicyOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether idempotency-key-based deduplication is enabled.
    /// </summary>
    public bool EnableDedupe { get; set; } = true;

    /// <summary>
    /// Gets or sets the sliding window for deduplication.
    /// Requests with the same idempotency key within this window are rejected as duplicates.
    /// </summary>
    public TimeSpan DedupeWindow { get; set; } = TimeSpan.FromMinutes(10);
}
