namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents a strongly-typed identifier for a notification, assigned at acceptance time.
/// </summary>
/// <remarks>
/// NotificationId is ULID-backed, providing uniqueness and chronological sortability.
/// Created once when a <see cref="NotificationRequest"/> is accepted and carried through
/// all downstream processing (envelope, delivery attempts, telemetry).
/// </remarks>
public readonly record struct NotificationId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationId"/> struct from a Ulid.
    /// </summary>
    /// <param name="value">The Ulid value.</param>
    public NotificationId(Ulid value)
    {
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationId"/> struct from a string.
    /// </summary>
    /// <param name="value">The Ulid string value.</param>
    /// <exception cref="ArgumentException">Thrown if the string is not a valid Ulid.</exception>
    public NotificationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

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
    /// Implicitly converts a NotificationId to a string.
    /// </summary>
    /// <param name="id">The NotificationId to convert.</param>
    public static implicit operator string(NotificationId id) => id.ToString();

    /// <summary>
    /// Implicitly converts a NotificationId to a Ulid.
    /// </summary>
    /// <param name="id">The NotificationId to convert.</param>
    public static implicit operator Ulid(NotificationId id) => id.Value;

    /// <summary>
    /// Creates a new NotificationId with a fresh Ulid.
    /// </summary>
    /// <returns>A new NotificationId.</returns>
    public static NotificationId NewId() => new(Ulid.NewUlid());

    /// <summary>
    /// Converts this NotificationId to a Ulid.
    /// </summary>
    /// <returns>The Ulid value.</returns>
    public Ulid ToUlid() => Value;

    /// <summary>
    /// Creates a NotificationId from a Ulid.
    /// </summary>
    /// <param name="ulid">The Ulid value.</param>
    /// <returns>A new NotificationId.</returns>
    public static NotificationId FromUlid(Ulid ulid) => new(ulid);

    /// <summary>
    /// Attempts to parse a string into a NotificationId.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <param name="id">The parsed NotificationId if successful.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    public static bool TryParse(string value, out NotificationId id)
    {
        if (Ulid.TryParse(value, out var ulid))
        {
            id = new NotificationId(ulid);
            return true;
        }

        id = default;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
