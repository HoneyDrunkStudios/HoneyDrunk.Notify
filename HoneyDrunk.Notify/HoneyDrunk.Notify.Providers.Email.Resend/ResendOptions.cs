namespace HoneyDrunk.Notify.Providers.Email.Resend;

/// <summary>
/// Configuration options for the Resend email provider.
/// </summary>
public sealed class ResendOptions
{
    /// <summary>
    /// Gets or sets the Resend API key used for authentication.
    /// This property is retained for source compatibility but is not used by the provider.
    /// Credentials are resolved from the <c>Resend--ApiKey</c> Vault secret for each send.
    /// </summary>
    [Obsolete("Resend credentials must be stored in Vault as Resend--ApiKey and are resolved at send time.")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default sender email address (the "From" header).
    /// Used when <see cref="Abstractions.Models.Email.EmailEnvelope.From"/> is not set.
    /// </summary>
    public string? FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the default sender display name.
    /// Used when <see cref="Abstractions.Models.Email.EmailEnvelope.FromDisplayName"/> is not set.
    /// </summary>
    public string? FromDisplayName { get; set; }
}
