using HoneyDrunk.Notify.Abstractions;

namespace HoneyDrunk.Notify.Queue.Abstractions;

/// <summary>
/// A single dead-lettered notification surfaced for inspection, replay, or purge.
/// </summary>
/// <param name="NotificationId">The original notification identifier.</param>
/// <param name="DeliveryCount">How many delivery attempts were made before dead-lettering.</param>
/// <param name="Reason">The human-readable reason the message was dead-lettered.</param>
/// <param name="Envelope">The original notification envelope for replay.</param>
public sealed record DeadLetterEntry(
    string NotificationId,
    int DeliveryCount,
    string Reason,
    NotificationEnvelope Envelope)
{
    /// <summary>
    /// Gets the template key from the envelope.
    /// </summary>
    public string TemplateKey => Envelope.TemplateKey.Value;

    /// <summary>
    /// Gets the channel name from the envelope.
    /// </summary>
    public string Channel => Envelope.Channel.ToString();

    /// <summary>
    /// Gets the UTC timestamp when the message was dead-lettered, if recorded.
    /// </summary>
    public DateTimeOffset? DeadLetteredAt { get; init; }

    /// <summary>
    /// Gets the correlation identifier from the envelope, if present.
    /// </summary>
    public string? CorrelationId => Envelope.CorrelationId;

    /// <summary>
    /// Gets the tenant identifier from the envelope, if present.
    /// </summary>
    public string? TenantId => Envelope.TenantId;
}
