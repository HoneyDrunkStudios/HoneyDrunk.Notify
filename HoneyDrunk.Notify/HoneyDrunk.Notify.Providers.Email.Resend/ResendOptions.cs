namespace HoneyDrunk.Notify.Providers.Email.Resend;

/// <summary>
/// Configuration options for the Resend email provider.
/// </summary>
public sealed class ResendOptions
{
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
