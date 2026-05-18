using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.Diagnostics;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace HoneyDrunk.Notify.Intake;

/// <summary>
/// Core notification gateway that validates requests, applies deduplication,
/// renders channel-specific payloads, builds envelopes, and enqueues them for delivery.
/// </summary>
#pragma warning disable CA1812
internal sealed partial class NotificationGateway(
    IOptions<NotifyRuntimeOptions> options,
    INotificationEnqueuer enqueuer,
    IIdempotencyStore idempotencyStore,
    IEmailTemplateRenderer emailTemplateRenderer,
    ILogger<NotificationGateway> logger) : INotificationGateway
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public async Task<NotificationOutcome> EnqueueAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = NotifyActivitySource.Source.StartActivity(
            NotifyEventNames.EnqueueAccepted, ActivityKind.Internal);

        var runtimeOptions = options.Value;
        var notificationId = NotificationId.NewId();
        var now = DateTimeOffset.UtcNow;

        activity?.SetTag("notification.id", notificationId.ToString());
        activity?.SetTag("channel", request.Channel.ToString());
        activity?.SetTag("template.key", request.TemplateKey.Value);

        if (!runtimeOptions.Enabled)
        {
            SetRejected(activity, "RuntimeDisabled");
            LogPipelineDisabled(
                logger,
                NotifyEventNames.EnqueueRejected,
                notificationId,
                request.Channel,
                request.TemplateKey.Value);
            return NotificationOutcome.Rejected(notificationId, now, RejectionReason.RuntimeDisabled, "Notification subsystem is disabled.");
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            SetRejected(activity, "ValidationFailed");
            LogValidationFailed(
                logger,
                NotifyEventNames.EnqueueRejected,
                notificationId,
                request.Channel,
                request.TemplateKey.Value,
                validationError);
            return NotificationOutcome.Rejected(notificationId, now, RejectionReason.ValidationFailed, validationError);
        }

        var effectiveRequest = request;

        if (runtimeOptions.EnableDedupe && effectiveRequest.IdempotencyKey.HasValue)
        {
            var key = effectiveRequest.IdempotencyKey.Value.Value;
            var claimed = await idempotencyStore.TryBeginAsync(key, now, runtimeOptions.DedupeWindow, cancellationToken);

            if (!claimed)
            {
                SetRejected(activity, "DuplicateIdempotencyKey");
                LogDuplicateIdempotencyKey(
                    logger,
                    NotifyEventNames.EnqueueRejected,
                    key,
                    notificationId);
                return NotificationOutcome.Rejected(notificationId, now, RejectionReason.DuplicateIdempotencyKey, $"Idempotency key '{key}' already used within the deduplication window.");
            }
        }

        var envelope = new NotificationEnvelope(
            notificationId,
            effectiveRequest.Channel,
            effectiveRequest.Recipient,
            effectiveRequest.TemplateKey,
            effectiveRequest.Model)
        {
            CorrelationId = Activity.Current?.Id,
            Priority = effectiveRequest.Priority,
            Tags = effectiveRequest.Tags,
            IdempotencyKey = effectiveRequest.IdempotencyKey,
            CreatedAtUtc = now,
            Payload = await RenderChannelPayloadAsync(effectiveRequest, cancellationToken),
        };

        await enqueuer.EnqueueAsync(envelope, cancellationToken);

        if (effectiveRequest.IdempotencyKey.HasValue)
        {
            await idempotencyStore.CompleteAsync(
                effectiveRequest.IdempotencyKey.Value.Value,
                notificationId.ToString(),
                cancellationToken);
        }

        activity?.SetTag("correlation.id", envelope.CorrelationId);

        LogEnqueueAccepted(
            logger,
            NotifyEventNames.EnqueueAccepted,
            notificationId,
            effectiveRequest.Channel,
            effectiveRequest.TemplateKey.Value,
            envelope.CorrelationId);

        return NotificationOutcome.Accepted(notificationId, now);
    }

    private static string? ValidateRequest(NotificationRequest request)
    {
        if (request.Recipient is null)
            return "Recipient is required.";

        if (string.IsNullOrWhiteSpace(request.Recipient.Address))
            return "Recipient address is required.";

        if (request.Channel == NotificationChannel.Email && !request.Recipient.Address.Contains('@', StringComparison.Ordinal))
            return "Email channel requires a valid email address containing '@'.";

        if (string.IsNullOrWhiteSpace(request.TemplateKey.Value))
            return "TemplateKey is required.";

        return null;
    }

    private static void SetRejected(Activity? activity, string reason)
    {
        if (activity is null)
        {
            return;
        }

        activity.DisplayName = NotifyEventNames.EnqueueRejected;
        activity.SetTag("rejection.reason", reason);
        activity.SetStatus(ActivityStatusCode.Error, reason);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Event}: Notification pipeline disabled. NotificationId={NotificationId}, Channel={Channel}, TemplateKey={TemplateKey}.")]
    private static partial void LogPipelineDisabled(
        ILogger logger,
        string @event,
        NotificationId notificationId,
        NotificationChannel channel,
        string templateKey);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Event}: NotificationId={NotificationId}, Channel={Channel}, TemplateKey={TemplateKey}, Detail={Detail}.")]
    private static partial void LogValidationFailed(
        ILogger logger,
        string @event,
        NotificationId notificationId,
        NotificationChannel channel,
        string templateKey,
        string? detail);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Event}: Duplicate IdempotencyKey={Key}, NotificationId={NotificationId}.")]
    private static partial void LogDuplicateIdempotencyKey(
        ILogger logger,
        string @event,
        string key,
        NotificationId notificationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Event}: NotificationId={NotificationId}, Channel={Channel}, TemplateKey={TemplateKey}, CorrelationId={CorrelationId}.")]
    private static partial void LogEnqueueAccepted(
        ILogger logger,
        string @event,
        NotificationId notificationId,
        NotificationChannel channel,
        string templateKey,
        string? correlationId);

    private async Task<object?> RenderChannelPayloadAsync(
        NotificationRequest request, CancellationToken ct)
    {
        if (request.Channel != NotificationChannel.Email)
            return null;

        var emailContent = await emailTemplateRenderer.RenderEmailAsync(
            request.TemplateKey, request.Model, ct);

        return new EmailEnvelope(request.Recipient.Address, emailContent);
    }
}
