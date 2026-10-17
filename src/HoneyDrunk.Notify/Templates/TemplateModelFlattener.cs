using System.Collections;
using System.Globalization;

namespace HoneyDrunk.Notify.Templates;

/// <summary>
/// Flattens a model dictionary into a flat <c>string → string</c> lookup suitable for token replacement.
/// Nested dictionaries are dot-separated (e.g. <c>Address.City</c>). Null values become empty strings.
/// </summary>
internal static class TemplateModelFlattener
{
    /// <summary>
    /// Flattens the model into a case-insensitive string dictionary.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Flatten(IReadOnlyDictionary<string, object?>? model)
    {
        if (model is null || model.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(model.Count, StringComparer.OrdinalIgnoreCase);
        FlattenRecursive(model, prefix: null, result);
        return result;
    }

    private static void FlattenRecursive(IReadOnlyDictionary<string, object?> source, string? prefix, Dictionary<string, string> result)
    {
        foreach (var (key, value) in source)
        {
            var fullKey = prefix is null ? key : $"{prefix}.{key}";

            switch (value)
            {
                case null:
                    result[fullKey] = string.Empty;
                    break;

                case IReadOnlyDictionary<string, object?> nested:
                    FlattenRecursive(nested, fullKey, result);
                    break;

                case IDictionary dict:
                    var wrapped = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (DictionaryEntry entry in dict)
                    {
                        wrapped[entry.Key?.ToString() ?? string.Empty] = entry.Value;
                    }

                    FlattenRecursive(wrapped, fullKey, result);
                    break;

                default:
                    result[fullKey] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                    break;
            }
        }
    }
}
