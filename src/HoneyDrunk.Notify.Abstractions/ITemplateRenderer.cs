namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Renders a notification template into a channel-specific payload.
/// </summary>
/// <remarks>
/// Implementations load a template identified by <see cref="TemplateKey"/>, apply the model
/// data, and return the rendered content as a string (HTML body, plain text, etc.).
/// The renderer is channel-agnostic; callers interpret the output based on the target channel.
/// </remarks>
public interface ITemplateRenderer
{
    /// <summary>
    /// Renders the specified template with the given model data.
    /// </summary>
    /// <param name="templateKey">The template to render.</param>
    /// <param name="model">The key-value data to inject into the template.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The rendered content as a string.</returns>
    Task<string> RenderAsync(
        TemplateKey templateKey,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default);
}
