namespace HoneyDrunk.Notify.Abstractions.Models.Email;

/// <summary>
/// Represents the rendered content of an email notification, containing the subject line,
/// body text, and format indicator.
/// </summary>
/// <param name="Subject">The rendered email subject line.</param>
/// <param name="Body">The rendered email body content.</param>
/// <param name="IsHtml">
/// <c>true</c> when the body contains HTML markup; <c>false</c> for plain-text body.
/// The SMTP provider uses this to set the correct MIME content type.
/// </param>
public sealed record EmailContent(string Subject, string Body, bool IsHtml = false);
