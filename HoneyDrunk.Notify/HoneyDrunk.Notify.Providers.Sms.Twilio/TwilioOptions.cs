namespace HoneyDrunk.Notify.Providers.Sms.Twilio;

/// <summary>
/// Configuration options for the Twilio SMS provider.
/// </summary>
public sealed class TwilioOptions
{
    /// <summary>
    /// Gets or sets the default sender phone number in E.164 format (e.g., "+15551234567").
    /// Used when <see cref="Abstractions.Models.Sms.SmsEnvelope.From"/> is not set.
    /// </summary>
    public string FromNumber { get; set; } = string.Empty;
}
