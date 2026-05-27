using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
internal sealed partial class EmailFileTemplateRenderer(
    IOptions<TemplateOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailFileTemplateRenderer> logger) : IEmailTemplateRenderer
#pragma warning restore CA1812
{
    private readonly TemplateFileLoader _loader = new(options, timeProvider);

    /// <inheritdoc />
    public async Task<EmailContent> RenderEmailAsync(
        TemplateKey templateKey,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default)
    {
        var values = TemplateModelFlattener.Flatten(model);

        var subjectTemplate = await _loader.LoadWithSuffixAsync(templateKey, ".subject.txt", cancellationToken);
        var subject = SimpleTokenReplacer.Replace(subjectTemplate, values);

        var (bodyTemplate, isHtml) = await LoadBodyTemplateAsync(templateKey, cancellationToken);
        var body = SimpleTokenReplacer.Replace(bodyTemplate, values);

        LogLoadedEmailTemplate(logger, templateKey, isHtml);

        return new EmailContent(subject, body, isHtml);
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Loaded email template '{TemplateKey}' with IsHtml={IsHtml}.")]
    private static partial void LogLoadedEmailTemplate(
        ILogger logger,
        TemplateKey templateKey,
        bool isHtml);

    private async Task<(string content, bool isHtml)> LoadBodyTemplateAsync(
        TemplateKey templateKey,
        CancellationToken cancellationToken)
    {
        if (_loader.ExistsWithSuffix(templateKey, ".body.html"))
        {
            var htmlContent = await _loader.LoadWithSuffixAsync(templateKey, ".body.html", cancellationToken);
            return (htmlContent, true);
        }

        var textContent = await _loader.LoadWithSuffixAsync(templateKey, ".body.txt", cancellationToken);
        return (textContent, false);
    }
}
