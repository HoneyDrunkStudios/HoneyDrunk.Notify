using HoneyDrunk.Notify.Queue.Abstractions;

namespace HoneyDrunk.Notify.Queue.AzureStorage;

/// <summary>
/// Configuration for the Azure Storage Queue adapter.
/// Extends <see cref="NotificationQueueOptions"/> with Azure-specific connection details.
/// </summary>
public sealed class AzureStorageQueueOptions : NotificationQueueOptions
{
    /// <summary>
    /// Gets or sets the Azure Storage connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to create the queue if it does not exist on first use.
    /// </summary>
    public bool CreateIfNotExists { get; set; } = true;
}
