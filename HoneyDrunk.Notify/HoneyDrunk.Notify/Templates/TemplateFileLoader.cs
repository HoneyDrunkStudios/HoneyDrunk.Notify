using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Options;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace HoneyDrunk.Notify.Templates;

/// <summary>
/// Shared safe file loader for Notify template renderers.
/// </summary>
internal sealed class TemplateFileLoader(IOptions<TemplateOptions> options, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loads a plain template using the configured default extension when the key has none.
    /// </summary>
    /// <param name="templateKey">The template key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The template file content.</returns>
    public Task<string> LoadAsync(TemplateKey templateKey, CancellationToken cancellationToken)
    {
        var templateOptions = options.Value;
        var relativePath = (string)templateKey;

        if (!relativePath.EndsWith(templateOptions.Extension, StringComparison.OrdinalIgnoreCase))
        {
            relativePath += templateOptions.Extension;
        }

        return LoadRelativeAsync(templateOptions, relativePath, templateKey, cancellationToken);
    }

    /// <summary>
    /// Loads a template using a renderer-specific suffix.
    /// </summary>
    /// <param name="templateKey">The template key.</param>
    /// <param name="suffix">The suffix appended to the template key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The template file content.</returns>
    public Task<string> LoadWithSuffixAsync(
        TemplateKey templateKey,
        string suffix,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);

        var templateOptions = options.Value;
        var relativePath = (string)templateKey + suffix;
        return LoadRelativeAsync(templateOptions, relativePath, templateKey, cancellationToken);
    }

    /// <summary>
    /// Checks whether a suffixed template file exists under the configured root.
    /// </summary>
    /// <param name="templateKey">The template key.</param>
    /// <param name="suffix">The suffix appended to the template key.</param>
    /// <returns><see langword="true" /> when the resolved file exists.</returns>
    public bool ExistsWithSuffix(TemplateKey templateKey, string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);

        var templateOptions = options.Value;
        var fullPath = ResolvePath(templateOptions.RootPath, (string)templateKey + suffix);
        return File.Exists(fullPath);
    }

    private static string ResolvePath(string rootPath, string relativePath)
    {
        var fullRootPath = Path.GetFullPath(rootPath);
        var fullPath = Path.GetFullPath(Path.Join(fullRootPath, relativePath));

        // Reject any path that resolves outside rootPath. Use Path.GetRelativePath rather than
        // StartsWith(rootPath) — the latter is bypassable when rootPath is a prefix of a sibling
        // directory name (e.g. /templates vs /templates_evil/x).
        var relative = Path.GetRelativePath(fullRootPath, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                $"Template path '{relativePath}' resolves outside the template root directory. Path traversal is not allowed.");
        }

        return fullPath;
    }

    private async Task<string> LoadRelativeAsync(
        TemplateOptions templateOptions,
        string relativePath,
        TemplateKey templateKey,
        CancellationToken cancellationToken)
    {
        var filePath = ResolvePath(templateOptions.RootPath, relativePath);

        if (templateOptions.CacheEnabled && _cache.TryGetValue(filePath, out var cached))
        {
            var age = timeProvider.GetUtcNow() - cached.LoadedAt;
            if (age < templateOptions.CacheTtl)
            {
                return cached.Content;
            }
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Template file not found: '{filePath}' (key: '{templateKey}').",
                filePath);
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        if (templateOptions.CacheEnabled)
        {
            _cache[filePath] = new CacheEntry(content, timeProvider.GetUtcNow());
        }

        return content;
    }

    private sealed record CacheEntry(string Content, DateTimeOffset LoadedAt);
}
