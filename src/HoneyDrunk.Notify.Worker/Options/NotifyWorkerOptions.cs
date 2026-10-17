namespace HoneyDrunk.Notify.Worker.Options;

/// <summary>
/// Configuration for the notification dispatch worker.
/// </summary>
public sealed class NotifyWorkerOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the dispatch worker is active.
    /// When <c>false</c>, the background service idles without polling.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval between queue poll cycles.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum number of envelopes dequeued per poll cycle.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets which queue adapter to use.
    /// "InMemory" for development, "AzureStorage" for production.
    /// </summary>
    public string QueueAdapter { get; set; } = "InMemory";
}
