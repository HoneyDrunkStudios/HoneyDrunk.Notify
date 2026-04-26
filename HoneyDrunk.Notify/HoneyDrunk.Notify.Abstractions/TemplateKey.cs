namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents a reference to a notification template used for content rendering.
/// </summary>
/// <remarks>
/// TemplateKey is the stable identifier callers use to select a template.
/// Format is free-form but conventionally dot-separated (e.g., "order.confirmation", "auth.password-reset").
/// Maximum length: 128 characters.
/// </remarks>
public readonly record struct TemplateKey
{
    private const int MaxLength = 128;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateKey"/> struct.
    /// </summary>
    /// <param name="value">The template key. Must be non-empty and at most 128 characters.</param>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty, or exceeds the maximum length.</exception>
    public TemplateKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"TemplateKey must be at most {MaxLength} characters.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the string value of this template key.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Implicitly converts a TemplateKey to a string.
    /// </summary>
    /// <param name="key">The TemplateKey to convert.</param>
    public static implicit operator string(TemplateKey key) => key.Value;

    /// <inheritdoc />
    public override string ToString() => Value;
}
