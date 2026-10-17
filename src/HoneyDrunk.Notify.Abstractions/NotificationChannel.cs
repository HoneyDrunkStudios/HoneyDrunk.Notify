namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents the delivery channel through which a notification is sent.
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// Email delivery (SMTP, transactional API, etc.).
    /// </summary>
    Email = 0,

    /// <summary>
    /// SMS delivery (Twilio, etc.).
    /// </summary>
    Sms = 1,
}
