namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents a strongly-typed identifier for a single delivery attempt against a provider.
/// </summary>
/// <remarks>
/// AttemptId is ULID-backed, providing uniqueness and chronological sortability.
/// A single notification may produce multiple attempts (retries), each with a distinct AttemptId.
/// </remarks>
public readonly record struct AttemptId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptId"/> struct from a Ulid.
    /// </summary>
    /// <param name="value">The Ulid value.</param>
    public AttemptId(Ulid value)
    {
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptId"/> struct from a string.
    /// </summary>
    /// <param name="value">The Ulid string value.</param>
    /// <exception cref="ArgumentException">Thrown if the string is not a valid Ulid.</exception>
    public AttemptId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Ulid.TryParse(value, out var ulid))
        {
            throw new ArgumentException("Value is not a valid ULID.", nameof(value));
        }

        Value = ulid;
    }

    /// <summary>
    /// Gets the Ulid value.
    /// </summary>
    public Ulid Value { get; }

    /// <summary>
    /// Implicitly converts an AttemptId to a string.
    /// </summary>
    /// <param name="id">The AttemptId to convert.</param>
    public static implicit operator string(AttemptId id) => id.ToString();

    /// <summary>
    /// Implicitly converts an AttemptId to a Ulid.
    /// </summary>
    /// <param name="id">The AttemptId to convert.</param>
    public static implicit operator Ulid(AttemptId id) => id.Value;

    /// <summary>
    /// Creates a new AttemptId with a fresh Ulid.
    /// </summary>
    /// <returns>A new AttemptId.</returns>
    public static AttemptId NewId() => new(Ulid.NewUlid());

    /// <summary>
    /// Converts this AttemptId to a Ulid.
    /// </summary>
    /// <returns>The Ulid value.</returns>
    public Ulid ToUlid() => Value;

    /// <summary>
    /// Creates an AttemptId from a Ulid.
    /// </summary>
    /// <param name="ulid">The Ulid value.</param>
    /// <returns>A new AttemptId.</returns>
    public static AttemptId FromUlid(Ulid ulid) => new(ulid);

    /// <summary>
    /// Attempts to parse a string into an AttemptId.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <param name="id">The parsed AttemptId if successful.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    public static bool TryParse(string value, out AttemptId id)
    {
        if (Ulid.TryParse(value, out var ulid))
        {
            id = new AttemptId(ulid);
            return true;
        }

        id = default;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
