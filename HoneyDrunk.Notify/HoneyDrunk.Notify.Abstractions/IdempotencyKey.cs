namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents a caller-supplied idempotency key used to prevent duplicate notification delivery.
/// </summary>
/// <remarks>
/// When a caller provides an IdempotencyKey, the notification subsystem uses it to detect
/// and reject requests that have already been accepted. The key is an opaque, case-sensitive
/// string with a maximum length of 256 characters.
/// </remarks>
public readonly record struct IdempotencyKey
{
    private const int MaxLength = 256;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyKey"/> struct.
    /// </summary>
    /// <param name="value">The idempotency key value. Must be non-empty and at most 256 characters.</param>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty, or exceeds the maximum length.</exception>
    public IdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"IdempotencyKey must be at most {MaxLength} characters.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the string value of this idempotency key.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Implicitly converts an IdempotencyKey to a string.
    /// </summary>
    /// <param name="key">The IdempotencyKey to convert.</param>
    public static implicit operator string(IdempotencyKey key) => key.Value;

    /// <inheritdoc />
    public override string ToString() => Value;
}
