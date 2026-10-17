namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Resolves the appropriate <see cref="INotificationSender"/> for a given delivery channel.
/// Used by the dispatcher to route notifications to channel-specific providers.
/// </summary>
public interface INotificationSenderResolver
{
    /// <summary>
    /// Returns the sender registered for the specified channel.
    /// </summary>
    /// <param name="channel">The delivery channel.</param>
    /// <returns>The sender for that channel.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no sender is registered for the channel.</exception>
    INotificationSender Resolve(NotificationChannel channel);
}
