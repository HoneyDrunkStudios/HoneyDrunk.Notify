namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents a caller's intent to send a notification.
/// </summary>
/// <remarks>
/// <para>
/// NotificationRequest is the public-facing input to <see cref="INotificationGateway"/>.
/// It captures what the caller wants to send, to whom, and through which channel, but
/// contains no delivery mechanics. The gateway validates, assigns a <see cref="NotificationId"/>,
/// and converts the request into a <see cref="NotificationEnvelope"/>
/// for the worker pipeline.
/// </para>
/// <para>
/// The <see cref="Model"/> property carries the template data payload. It is typed as
/// <c>IReadOnlyDictionary&lt;string, object?&gt;</c> so callers can pass any key-value data
/// the template renderer needs, while keeping the contract serialization-friendly.
/// </para>
/// </remarks>
/// <param name="Channel">The delivery channel (e.g., Email).</param>
/// <param name="Recipient">The channel-specific recipient.</param>
/// <param name="TemplateKey">The template to render for this notification.</param>
/// <param name="Model">The template data payload (key-value pairs for the renderer).</param>
public sealed record NotificationRequest(
    NotificationChannel Channel,
    Recipient Recipient,
    TemplateKey TemplateKey,
    IReadOnlyDictionary<string, object?> Model)
{
    /// <summary>
    /// Gets the optional idempotency key for duplicate detection.
    /// </summary>
    public IdempotencyKey? IdempotencyKey { get; init; }

    /// <summary>
    /// Gets the priority level. Defaults to <see cref="NotificationPriority.Normal"/>.
    /// </summary>
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    /// <summary>
    /// Gets optional tags for routing, filtering, and analytics.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
