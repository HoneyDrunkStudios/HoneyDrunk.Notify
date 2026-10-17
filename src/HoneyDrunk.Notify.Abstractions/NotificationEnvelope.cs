namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Represents a fully materialized notification ready for delivery by the worker pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The envelope is the internal hand-off object between the gateway (intake) and the sender
/// (delivery). It carries the original request data plus Grid context fields propagated from
/// <c>IGridContext</c> and <c>IOperationContext</c> so that the worker can restore distributed
/// tracing, tenant isolation, and telemetry without access to the original HTTP scope.
/// </para>
/// <para>
/// Envelope instances are typically serialized into a queue or outbox and deserialized by
/// the worker. All properties are therefore designed to be serialization-friendly.
/// </para>
/// </remarks>
/// <param name="NotificationId">The unique identifier assigned to this notification at acceptance.</param>
/// <param name="Channel">The target delivery channel.</param>
/// <param name="Recipient">The channel-specific recipient.</param>
/// <param name="TemplateKey">The template to render for content generation.</param>
/// <param name="Model">The template data payload.</param>
public sealed record NotificationEnvelope(
    NotificationId NotificationId,
    NotificationChannel Channel,
    Recipient Recipient,
    TemplateKey TemplateKey,
    IReadOnlyDictionary<string, object?> Model)
{
    // --- Grid context (propagated from the originating request scope) ---

    /// <summary>
    /// Gets the correlation identifier from the originating <c>IGridContext</c>.
    /// Groups all operations in the same request tree across the Grid.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the causation identifier from the originating <c>IGridContext</c>.
    /// Indicates which operation triggered this notification.
    /// </summary>
    public string? CausationId { get; init; }

    /// <summary>
    /// Gets the Node identifier that accepted the notification.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// Gets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Gets the environment in which the notification was enqueued.
    /// </summary>
    public string? Environment { get; init; }

    // --- Notification metadata ---

    /// <summary>
    /// Gets the priority level.
    /// </summary>
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    /// <summary>
    /// Gets optional tags for routing, filtering, and analytics.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the optional idempotency key for duplicate detection.
    /// </summary>
    public IdempotencyKey? IdempotencyKey { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the envelope was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets the channel-specific rendered payload.
    /// For the Email channel this is an <see cref="Models.Email.EmailEnvelope"/> containing
    /// the rendered subject, body, and sender/recipient details so the provider can send
    /// without re-rendering.
    /// </summary>
    public object? Payload { get; init; }
}
