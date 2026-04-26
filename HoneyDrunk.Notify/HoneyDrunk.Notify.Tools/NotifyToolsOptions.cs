namespace HoneyDrunk.Notify.Tools;

/// <summary>
/// Parsed CLI options for DLQ tooling operations.
/// </summary>
internal sealed class NotifyToolsOptions
{
    /// <summary>
    /// Gets or sets the queue adapter: "AzureStorage" or "InMemory".
    /// </summary>
    public string Adapter { get; set; } = "AzureStorage";

    /// <summary>
    /// Gets or sets the main queue name.
    /// </summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dead-letter queue name. Defaults to QueueName + "-dlq".
    /// </summary>
    public string? DeadLetterQueueName { get; set; }

    /// <summary>
    /// Gets the effective DLQ name.
    /// </summary>
    public string EffectiveDeadLetterQueueName => DeadLetterQueueName ?? $"{QueueName}-dlq";

    /// <summary>
    /// Gets or sets the connection string (required for AzureStorage adapter).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items to list. Defaults to 25.
    /// </summary>
    public int ListTake { get; set; } = 25;

    /// <summary>
    /// Gets or sets a value indicating whether to perform a dry run (print actions without executing).
    /// </summary>
    public bool DryRun { get; set; }
}
