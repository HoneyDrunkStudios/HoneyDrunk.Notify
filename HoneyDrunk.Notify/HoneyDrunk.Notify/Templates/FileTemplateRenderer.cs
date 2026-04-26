using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace HoneyDrunk.Notify.Templates;

/// <summary>
/// File-based <see cref="ITemplateRenderer"/> that loads plain-text templates from disk
/// and performs <c>{{Token}}</c> replacement using the provided model.
/// </summary>
#pragma warning disable CA1812
internal sealed class FileTemplateRenderer(
    IOptions<TemplateOptions> options,
    TimeProvider timeProvider,
    ILogger<FileTemplateRenderer> logger) : ITemplateRenderer
#pragma warning restore CA1812
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<string> RenderAsync(
        TemplateKey templateKey,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default)
    {
        var templateContent = await LoadTemplateAsync(templateKey, cancellationToken);
        var values = TemplateModelFlattener.Flatten(model);
        return SimpleTokenReplacer.Replace(templateContent, values);
    }

    /// <summary>
    /// Resolves the template key to a full file path, blocking path traversal attempts.
    /// </summary>
    private static string ResolveTemplatePath(TemplateKey templateKey, TemplateOptions templateOptions)
    {
        var rootPath = Path.GetFullPath(templateOptions.RootPath);

        var relativePath = (string)templateKey;
        if (!relativePath.EndsWith(templateOptions.Extension, StringComparison.OrdinalIgnoreCase))
            relativePath += templateOptions.Extension;

        var fullPath = Path.GetFullPath(Path.Join(rootPath, relativePath));

        // Reject any path that resolves outside rootPath. Use Path.GetRelativePath rather than
        // StartsWith(rootPath) — the latter is bypassable when rootPath is a prefix of a sibling
        // directory name (e.g. /templates vs /templates_evil/x).
        var relative = Path.GetRelativePath(rootPath, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                $"Template key '{templateKey}' resolves outside the template root directory. Path traversal is not allowed.");
        }

        return fullPath;
    }

    private async Task<string> LoadTemplateAsync(TemplateKey templateKey, CancellationToken ct)
    {
        var templateOptions = options.Value;
        var filePath = ResolveTemplatePath(templateKey, templateOptions);

        if (templateOptions.CacheEnabled && _cache.TryGetValue(filePath, out var cached))
        {
            var age = timeProvider.GetUtcNow() - cached.LoadedAt;

            if (age < templateOptions.CacheTtl)
                return cached.Content;
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Template file not found: '{filePath}' (key: '{templateKey}').", filePath);
        }

        var content = await File.ReadAllTextAsync(filePath, ct);

        if (templateOptions.CacheEnabled)
        {
            _cache[filePath] = new CacheEntry(content, timeProvider.GetUtcNow());
            logger.LogDebug("Cached template '{TemplateKey}' from '{Path}'.", (string)templateKey, filePath);
        }

        return content;
    }

    private sealed record CacheEntry(string Content, DateTimeOffset LoadedAt);
}
