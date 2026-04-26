using HoneyDrunk.Notify.Abstractions;

namespace HoneyDrunk.Notify.Hosting.AspNetCore.Options;

/// <summary>
/// Top-level configuration for the HoneyDrunk.Notify subsystem.
/// </summary>
public sealed class NotifyOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the notification subsystem is enabled.
    /// When <c>false</c>, the gateway rejects all requests and the health contributor reports unhealthy.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the default provider name used when no channel-specific mapping exists.
    /// </summary>
    public string DefaultProvider { get; set; } = "smtp";

    /// <summary>
    /// Gets or sets the default delivery channel for notifications that do not specify one explicitly.
    /// </summary>
    public NotificationChannel DefaultChannel { get; set; } = NotificationChannel.Email;

    /// <summary>
    /// Gets the optional channel-to-provider mapping.
    /// When a channel has no explicit mapping, <see cref="DefaultProvider"/> is used.
    /// </summary>
    public IDictionary<NotificationChannel, string> ProviderByChannel { get; } =
        new Dictionary<NotificationChannel, string>();

    /// <summary>
    /// Gets or sets retry behaviour for failed delivery attempts.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Gets or sets notification policy options (deduplication, suppression, etc.).
    /// </summary>
    public PolicyOptions Policy { get; set; } = new();

    /// <summary>
    /// Gets or sets template rendering options.
    /// </summary>
    public TemplateOptions Templates { get; set; } = new();
}
