using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace HoneyDrunk.Notify.Templates;

/// <summary>
/// File-based <see cref="IEmailTemplateRenderer"/> that loads a subject template
/// (<c>{key}.subject.txt</c>) and a body template (<c>{key}.body.html</c> or
/// <c>{key}.body.txt</c>) from disk, applying <c>{{Token}}</c> replacement.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>If <c>{key}.body.html</c> exists, it is used and <see cref="EmailContent.IsHtml"/> is <c>true</c>.</item>
///   <item>Otherwise <c>{key}.body.txt</c> is loaded as plain-text.</item>
///   <item><c>{key}.subject.txt</c> is always required.</item>
/// </list>
/// Caching follows the same <see cref="TemplateOptions"/> TTL as <see cref="FileTemplateRenderer"/>.
/// </remarks>
#pragma warning disable CA1812
internal sealed class EmailFileTemplateRenderer(
    IOptions<TemplateOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailFileTemplateRenderer> logger) : IEmailTemplateRenderer
#pragma warning restore CA1812
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<EmailContent> RenderEmailAsync(
        TemplateKey templateKey,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default)
    {
        var values = TemplateModelFlattener.Flatten(model);

        var subjectTemplate = await LoadTemplateFileAsync(templateKey, ".subject.txt", cancellationToken);
        var subject = SimpleTokenReplacer.Replace(subjectTemplate, values);

        var (bodyTemplate, isHtml) = await LoadBodyTemplateAsync(templateKey, cancellationToken);
        var body = SimpleTokenReplacer.Replace(bodyTemplate, values);

        return new EmailContent(subject, body, isHtml);
    }

    private static string ResolvePath(string rootPath, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));

        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Template path '{relativePath}' resolves outside the template root directory. Path traversal is not allowed.");
        }

        return fullPath;
    }

    private async Task<(string content, bool isHtml)> LoadBodyTemplateAsync(
        TemplateKey templateKey, CancellationToken ct)
    {
        var templateOptions = options.Value;
        var rootPath = Path.GetFullPath(templateOptions.RootPath);

        var htmlPath = ResolvePath(rootPath, (string)templateKey + ".body.html");

        if (File.Exists(htmlPath))
        {
            var content = await LoadCachedAsync(htmlPath, templateKey, ".body.html", ct);
            return (content, true);
        }

        var txtPath = ResolvePath(rootPath, (string)templateKey + ".body.txt");
        var txtContent = await LoadCachedAsync(txtPath, templateKey, ".body.txt", ct);
        return (txtContent, false);
    }

    private async Task<string> LoadTemplateFileAsync(
        TemplateKey templateKey, string suffix, CancellationToken ct)
    {
        var templateOptions = options.Value;
        var rootPath = Path.GetFullPath(templateOptions.RootPath);
        var filePath = ResolvePath(rootPath, (string)templateKey + suffix);

        return await LoadCachedAsync(filePath, templateKey, suffix, ct);
    }

    private async Task<string> LoadCachedAsync(
        string filePath, TemplateKey templateKey, string suffix, CancellationToken ct)
    {
        var templateOptions = options.Value;

        if (templateOptions.CacheEnabled && _cache.TryGetValue(filePath, out var cached))
        {
            var age = timeProvider.GetUtcNow() - cached.LoadedAt;
            if (age < templateOptions.CacheTtl)
                return cached.Content;
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Email template file not found: '{filePath}' (key: '{templateKey}', suffix: '{suffix}').",
                filePath);
        }

        var content = await File.ReadAllTextAsync(filePath, ct);

        if (templateOptions.CacheEnabled)
        {
            _cache[filePath] = new CacheEntry(content, timeProvider.GetUtcNow());
            logger.LogDebug("Cached email template '{TemplateKey}{Suffix}' from '{Path}'.", (string)templateKey, suffix, filePath);
        }

        return content;
    }

    private sealed record CacheEntry(string Content, DateTimeOffset LoadedAt);
}
