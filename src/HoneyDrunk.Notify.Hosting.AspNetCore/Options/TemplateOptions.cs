namespace HoneyDrunk.Notify.Hosting.AspNetCore.Options;

/// <summary>
/// Hosting-level configuration for notification template rendering.
/// Mapped into the core <see cref="Notify.Options.TemplateOptions"/> at registration time.
/// </summary>
public sealed class TemplateOptions
{
    /// <summary>
    /// Gets or sets the root directory where template files are located.
    /// When <c>null</c>, defaults to <c>{AppContext.BaseDirectory}/templates</c>.
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>
    /// Gets or sets the file extension appended to template keys. Include the leading dot.
    /// </summary>
    public string Extension { get; set; } = ".txt";

    /// <summary>
    /// Gets or sets a value indicating whether parsed template content is cached in memory.
    /// </summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how long cached template content remains valid.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);
}
