using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.Diagnostics;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace HoneyDrunk.Notify.Orchestration;

/// <summary>
/// Core notification gateway that validates requests, evaluates policies,
/// applies deduplication, renders channel-specific payloads, builds envelopes,
/// and enqueues them for delivery.
/// </summary>
#pragma warning disable CA1812
internal sealed class NotificationGateway(
    IOptions<NotifyRuntimeOptions> options,
    INotificationEnqueuer enqueuer,
    INotificationPolicy policy,
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
            SetRejected(activity, "PolicyDenied");
            logger.LogWarning(
                "{Event}: Notification pipeline disabled. NotificationId={NotificationId}, Channel={Channel}, TemplateKey={TemplateKey}.",
                NotifyEventNames.EnqueueRejected,
                notificationId,
                request.Channel,
                request.TemplateKey.Value);
            return NotificationOutcome.Rejected(notificationId, now, RejectionReason.PolicyDenied, "Notification subsystem is disabled.");
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            SetRejected(activity, "ValidationFailed");
            logger.LogWarning(
                "{Event}: NotificationId={NotificationId}, Channel={Channel}, TemplateKey={TemplateKey}, Detail={Detail}.",
                NotifyEventNames.EnqueueRejected,
                notificationId,
                request.Channel,
                request.TemplateKey.Value,
                validationError);
            return NotificationOutcome.Rejected(notificationId, now, RejectionReason.ValidationFailed, validationError);
        }

        var policyResult = await policy.EvaluateAsync(request, cancellationToken);
        if (!policyResult.IsAllowed)
        {
            SetRejected(activity, policyResult.RejectionReason.ToString());
            logger.LogInformation(
                "{Event}: NotificationId={NotificationId}, Channel={Channel}, TemplateKey={TemplateKey}, Reason={Reason}.",
                NotifyEventNames.EnqueueRejected,
                notificationId,
                request.Channel,
                request.TemplateKey.Value,
                policyResult.Detail);
            return NotificationOutcome.Rejected(notificationId, now, policyResult.RejectionReason, policyResult.Detail);
        }

        var effectiveRequest = policyResult.TransformedRequest ?? request;

        if (runtimeOptions.EnableDedupe && effectiveRequest.IdempotencyKey.HasValue)
        {
            var key = effectiveRequest.IdempotencyKey.Value.Value;
            var claimed = await idempotencyStore.TryBeginAsync(key, now, runtimeOptions.DedupeWindow, cancellationToken);

            if (!claimed)
            {
                SetRejected(activity, "DuplicateIdempotencyKey");
                logger.LogInformation(
                    "{Event}: Duplicate IdempotencyKey={Key}, NotificationId={NotificationId}.",
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

        logger.LogInformation(
            "{Event}: NotificationId={NotificationId}, Channel={Channel}, TemplateKey={TemplateKey}, CorrelationId={CorrelationId}.",
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
