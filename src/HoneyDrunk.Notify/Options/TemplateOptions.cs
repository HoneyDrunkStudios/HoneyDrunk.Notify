namespace HoneyDrunk.Notify.Options;

/// <summary>
/// Configuration for file-based template rendering in the core runtime.
/// </summary>
public sealed class TemplateOptions
{
    /// <summary>
    /// Gets or sets the root directory where template files are located.
    /// Defaults to "templates". Resolved relative to the application base directory at runtime.
    /// </summary>
    public string RootPath { get; set; } = "templates";

    /// <summary>
    /// Gets or sets the file extension appended to the template key when resolving files.
    /// Include the leading dot.
    /// </summary>
    public string Extension { get; set; } = ".txt";

    /// <summary>
    /// Gets or sets a value indicating whether parsed template content is cached in memory.
    /// </summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how long cached template content remains valid before being reloaded from disk.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);
}
