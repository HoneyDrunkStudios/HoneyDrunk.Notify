using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Sms;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace HoneyDrunk.Notify.Providers.Sms.Twilio;

/// <summary>
/// Sends SMS notifications via the Twilio REST API.
/// Reads the rendered <see cref="SmsEnvelope"/> from <see cref="NotificationEnvelope.Payload"/>.
/// </summary>
#pragma warning disable CA1812
internal sealed partial class TwilioNotificationSender(
    ISecretStore secretStore,
    IOptions<TwilioOptions> options,
    ILogger<TwilioNotificationSender> logger) : INotificationSender
#pragma warning restore CA1812
{
    private const string ProviderName = "twilio";
    private const string AccountSidSecretName = "Twilio--AccountSid";
    private const string AuthTokenSecretName = "Twilio--AuthToken";

    /// <inheritdoc />
    public async Task<DeliveryOutcome> SendAsync(
        NotificationEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var attemptId = AttemptId.NewId();

        if (envelope.Payload is not SmsEnvelope smsEnvelope)
        {
            LogMissingPayload(logger, envelope.NotificationId);

            return DeliveryOutcome.Failed(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                FailureKind.Permanent,
                "Missing or invalid SmsEnvelope payload on the notification envelope.");
        }

        var twilioOptions = options.Value;
        var fromNumber = smsEnvelope.From ?? twilioOptions.FromNumber;

        if (string.IsNullOrWhiteSpace(fromNumber))
        {
            return DeliveryOutcome.Failed(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                FailureKind.Permanent,
                "No sender phone number configured. Set TwilioOptions.FromNumber or SmsEnvelope.From.");
        }

        try
        {
            var accountSid = await GetSecretValueAsync(AccountSidSecretName, cancellationToken);
            var authToken = await GetSecretValueAsync(AuthTokenSecretName, cancellationToken);
            var client = new TwilioRestClient(accountSid, authToken);

            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(smsEnvelope.To),
                from: new PhoneNumber(fromNumber),
                body: smsEnvelope.Body,
                client: client);

            LogSent(logger, envelope.NotificationId, smsEnvelope.To, message.Sid);

            return DeliveryOutcome.Succeeded(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                message.Sid);
        }
        catch (global::Twilio.Exceptions.ApiException ex) when (IsTransient(ex))
        {
            LogTransientFailure(logger, ex, envelope.NotificationId, smsEnvelope.To);

            return DeliveryOutcome.Failed(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                FailureKind.Transient,
                ex.Message);
        }
        catch (global::Twilio.Exceptions.ApiException ex)
        {
            LogPermanentFailure(logger, ex, envelope.NotificationId, smsEnvelope.To);

            return DeliveryOutcome.Failed(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                FailureKind.Permanent,
                ex.Message);
        }
    }

    // Twilio HTTP 429 or 5xx are transient
    private static bool IsTransient(global::Twilio.Exceptions.ApiException ex) =>
        ex.Status is 429 or >= 500;

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Notification {NotificationId} has no SmsEnvelope payload. Cannot send via Twilio.")]
    private static partial void LogMissingPayload(ILogger logger, NotificationId notificationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Notification {NotificationId} sent via Twilio to {To}. SID: {MessageSid}.")]
    private static partial void LogSent(
        ILogger logger,
        NotificationId notificationId,
        string to,
        string messageSid);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Transient Twilio failure for {NotificationId} to {To}.")]
    private static partial void LogTransientFailure(
        ILogger logger,
        Exception exception,
        NotificationId notificationId,
        string to);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Permanent Twilio failure for {NotificationId} to {To}.")]
    private static partial void LogPermanentFailure(
        ILogger logger,
        Exception exception,
        NotificationId notificationId,
        string to);

    private async Task<string> GetSecretValueAsync(string secretName, CancellationToken cancellationToken)
    {
        var secret = await secretStore.GetSecretAsync(new SecretIdentifier(secretName), cancellationToken);
        return secret.Value;
    }
}
