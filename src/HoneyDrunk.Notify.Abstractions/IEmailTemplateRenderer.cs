using HoneyDrunk.Notify.Abstractions.Models.Email;

namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Renders email-specific content (subject + body) from a template key and model,
/// producing a ready-to-send <see cref="EmailContent"/>.
/// </summary>
/// <remarks>
/// Unlike the general-purpose <see cref="ITemplateRenderer"/> which returns a single string,
/// this interface returns a structured <see cref="EmailContent"/> with distinct subject and body,
/// and an <see cref="EmailContent.IsHtml"/> flag for MIME type selection.
/// </remarks>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders the email subject and body from the specified template, applying the model data.
    /// </summary>
    /// <param name="templateKey">The template identifier used to locate subject and body template files.</param>
    /// <param name="model">Template data payload for token replacement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A fully rendered <see cref="EmailContent"/> containing subject, body, and format indicator.</returns>
    Task<EmailContent> RenderEmailAsync(
        TemplateKey templateKey,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default);
}
