namespace HoneyDrunk.Notify.Providers.Sms.Twilio;

/// <summary>
/// Configuration options for the Twilio SMS provider.
/// </summary>
public sealed class TwilioOptions
{
    /// <summary>
    /// Gets or sets the Twilio Account SID.
    /// This property is retained for source compatibility but is not used by the provider.
    /// Credentials are resolved from the <c>Twilio--AccountSid</c> Vault secret for each send.
    /// </summary>
    [Obsolete("Twilio credentials must be stored in Vault as Twilio--AccountSid and Twilio--AuthToken and are resolved at send time.")]
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Twilio Auth Token.
    /// This property is retained for source compatibility but is not used by the provider.
    /// Credentials are resolved from the <c>Twilio--AuthToken</c> Vault secret for each send.
    /// </summary>
    [Obsolete("Twilio credentials must be stored in Vault as Twilio--AccountSid and Twilio--AuthToken and are resolved at send time.")]
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default sender phone number in E.164 format (e.g., "+15551234567").
    /// Used when <see cref="Abstractions.Models.Sms.SmsEnvelope.From"/> is not set.
    /// </summary>
    public string FromNumber { get; set; } = string.Empty;
}
