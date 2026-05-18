using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.Templates;

/// <summary>
/// File-based <see cref="ITemplateRenderer"/> that loads plain-text templates from disk
/// and performs <c>{{Token}}</c> replacement using the provided model.
/// </summary>
#pragma warning disable CA1812
internal sealed partial class FileTemplateRenderer(
    IOptions<TemplateOptions> options,
    TimeProvider timeProvider,
    ILogger<FileTemplateRenderer> logger) : ITemplateRenderer
#pragma warning restore CA1812
{
    private readonly TemplateFileLoader _loader = new(options, timeProvider);

    /// <inheritdoc />
    public async Task<string> RenderAsync(
        TemplateKey templateKey,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default)
    {
        var templateContent = await _loader.LoadAsync(templateKey, cancellationToken);
        LogLoadedTemplate(logger, (string)templateKey);
        var values = TemplateModelFlattener.Flatten(model);
        return SimpleTokenReplacer.Replace(templateContent, values);
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Loaded template '{TemplateKey}'.")]
    private static partial void LogLoadedTemplate(
        ILogger logger,
        string templateKey);
}
