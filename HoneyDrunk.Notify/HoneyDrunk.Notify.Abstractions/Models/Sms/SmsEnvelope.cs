namespace HoneyDrunk.Notify.Abstractions.Models.Sms;

/// <summary>
/// Fully resolved SMS payload attached to a <see cref="NotificationEnvelope"/> as its <c>Payload</c>.
/// Carries the recipient phone number and message body so the SMS provider can send without
/// re-rendering.
/// </summary>
/// <param name="To">The recipient phone number in E.164 format (e.g., "+15551234567").</param>
/// <param name="Body">The rendered message body. SMS providers typically limit this to 1600 characters.</param>
public sealed record SmsEnvelope(string To, string Body)
{
    /// <summary>
    /// Gets the sender phone number or short code override.
    /// When <c>null</c>, the provider falls back to its configured default sender
    /// (e.g., <c>TwilioOptions.FromNumber</c>).
    /// </summary>
    public string? From { get; init; }
}
