namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Describes why a <see cref="NotificationRequest"/> was rejected at intake.
/// </summary>
public enum RejectionReason
{
    /// <summary>
    /// No rejection — the request was accepted.
    /// </summary>
    None = 0,

    /// <summary>
    /// The request failed structural validation (missing fields, bad format, etc.).
    /// </summary>
    ValidationFailed = 1,

    /// <summary>
    /// A notification policy denied the request (rate-limit, opt-out, suppression rule, etc.).
    /// </summary>
    PolicyDenied = 2,

    /// <summary>
    /// A notification with the same idempotency key was already processed.
    /// </summary>
    DuplicateIdempotencyKey = 3,

    /// <summary>
    /// The requested channel is not configured or not available.
    /// </summary>
    ChannelUnavailable = 4,

    /// <summary>
    /// The requested template was not found.
    /// </summary>
    TemplateNotFound = 5,
}
