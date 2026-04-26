namespace HoneyDrunk.Notify.Abstractions.Models.Email;

/// <summary>
/// Fully resolved email payload attached to a <see cref="NotificationEnvelope"/> as its <c>Payload</c>.
/// Carries sender, recipient, rendered content, and optional headers so the SMTP provider can
/// construct the message deterministically without re-rendering.
/// </summary>
/// <param name="To">The recipient email address.</param>
/// <param name="Content">The rendered subject and body.</param>
public sealed record EmailEnvelope(string To, EmailContent Content)
{
    /// <summary>
    /// Gets the sender address override. When <c>null</c>, the provider falls back to
    /// its configured default sender address (e.g. <c>SmtpOptions.FromAddress</c>).
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    /// Gets the sender display name override.
    /// </summary>
    public string? FromDisplayName { get; init; }

    /// <summary>
    /// Gets optional custom SMTP headers (e.g. <c>Reply-To</c>, <c>X-Mailer</c>).
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}
