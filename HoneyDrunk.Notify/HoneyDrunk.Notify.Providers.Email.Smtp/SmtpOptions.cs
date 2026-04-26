namespace HoneyDrunk.Notify.Providers.Email.Smtp;

/// <summary>
/// Configuration for the SMTP email notification provider.
/// </summary>
public sealed class SmtpOptions
{
    /// <summary>
    /// Gets or sets the SMTP server hostname.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the SMTP server port.
    /// </summary>
    public int Port { get; set; } = 25;

    /// <summary>
    /// Gets or sets a value indicating whether to use TLS/SSL for the SMTP connection.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Gets or sets the SMTP authentication username.
    /// This property is retained for source compatibility but is not used by the provider.
    /// Credentials are resolved from the <c>Smtp--Username</c> Vault secret for each send.
    /// </summary>
    [Obsolete("SMTP credentials must be stored in Vault as Smtp--Username and Smtp--Password and are resolved at send time.")]
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the SMTP authentication password.
    /// This property is retained for source compatibility but is not used by the provider.
    /// Credentials are resolved from the <c>Smtp--Password</c> Vault secret for each send.
    /// </summary>
    [Obsolete("SMTP credentials must be stored in Vault as Smtp--Username and Smtp--Password and are resolved at send time.")]
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the default sender email address (the "From" header).
    /// </summary>
    public string? FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the display name for the sender.
    /// </summary>
    public string? FromDisplayName { get; set; }
}
