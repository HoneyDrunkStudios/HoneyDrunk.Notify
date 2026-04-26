namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents a channel-specific recipient address for notification delivery.
/// </summary>
/// <remarks>
/// <para>
/// Recipient wraps a channel and its corresponding address, keeping the two together
/// so downstream code does not need to interpret addresses without knowing the channel.
/// </para>
/// <para>
/// Examples:
/// <list type="bullet">
/// <item><see cref="NotificationChannel.Email"/>: "user@example.com"</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="Channel">The delivery channel this address targets.</param>
/// <param name="Address">The channel-specific address (e.g., email address, phone number).</param>
public sealed record Recipient(NotificationChannel Channel, string Address)
{
    /// <summary>
    /// Creates an email recipient.
    /// </summary>
    /// <param name="emailAddress">The email address.</param>
    /// <returns>A new <see cref="Recipient"/> targeting the email channel.</returns>
    public static Recipient Email(string emailAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress, nameof(emailAddress));
        return new Recipient(NotificationChannel.Email, emailAddress);
    }
}
