using System.Text.RegularExpressions;

namespace HoneyDrunk.Notify.Templates;

/// <summary>
/// Replaces <c>{{TokenName}}</c> placeholders in a template string with values from a dictionary.
/// Token names may contain letters, digits, underscores, and dots (e.g. <c>{{Address.City}}</c>) to
/// match the dot-separated keys produced by <see cref="TemplateModelFlattener"/>. Missing tokens are
/// left unchanged.
/// </summary>
internal static partial class SimpleTokenReplacer
{
    /// <summary>
    /// Replaces all recognized tokens in <paramref name="template"/> with corresponding values.
    /// </summary>
    /// <param name="template">The template text containing <c>{{Key}}</c> placeholders.</param>
    /// <param name="values">Token name → replacement value mapping.</param>
    /// <returns>The template with matched tokens replaced; unmatched tokens remain as-is.</returns>
    internal static string Replace(string template, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        return TokenPattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return values.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    // Match {{AlphaNumeric_Underscore.Dot}} tokens. Dots support nested keys from TemplateModelFlattener.
    [GeneratedRegex(@"\{\{([A-Za-z0-9_.]+)\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
