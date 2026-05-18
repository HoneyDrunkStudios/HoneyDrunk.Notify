using global::Resend;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.Providers.Email.Resend;

/// <summary>
/// Sends email notifications via the Resend HTTP API.
/// Reads the rendered <see cref="EmailEnvelope"/> from <see cref="NotificationEnvelope.Payload"/>.
/// </summary>
#pragma warning disable CA1812
internal sealed partial class ResendNotificationSender(
    IHttpClientFactory httpClientFactory,
    ISecretStore secretStore,
    IOptions<ResendOptions> options,
    ILogger<ResendNotificationSender> logger) : INotificationSender
#pragma warning restore CA1812
{
    private const string ProviderName = "resend";
    private const string ApiKeySecretName = "Resend--ApiKey";
    private const string HttpClientName = "HoneyDrunk.Notify.Resend";

    /// <inheritdoc />
    public async Task<DeliveryOutcome> SendAsync(
        NotificationEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var attemptId = AttemptId.NewId();

        if (envelope.Payload is not EmailEnvelope emailEnvelope)
        {
            LogMissingPayload(logger, envelope.NotificationId);

            return DeliveryOutcome.Failed(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                FailureKind.Permanent,
                "Missing or invalid EmailEnvelope payload on the notification envelope.");
        }

        var resendOptions = options.Value;
        var fromAddress = emailEnvelope.From ?? resendOptions.FromAddress;

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            return DeliveryOutcome.Failed(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                FailureKind.Permanent,
                "No sender address configured. Set ResendOptions.FromAddress or EmailEnvelope.From.");
        }

        var fromDisplayName = emailEnvelope.FromDisplayName ?? resendOptions.FromDisplayName;

        try
        {
            var apiKey = await GetSecretValueAsync(ApiKeySecretName, cancellationToken);
            var resendClient = CreateResendClient(apiKey);
            var message = new EmailMessage
            {
                From = new EmailAddress
                {
                    Email = fromAddress,
                    DisplayName = fromDisplayName,
                },
                Subject = emailEnvelope.Content.Subject,
            };

            message.To.Add(new EmailAddress { Email = emailEnvelope.To });

            if (emailEnvelope.Content.IsHtml)
            {
                message.HtmlBody = emailEnvelope.Content.Body;
            }
            else
            {
                message.TextBody = emailEnvelope.Content.Body;
            }

            if (emailEnvelope.Headers is not null && message.Headers is not null)
            {
                foreach (var (key, value) in emailEnvelope.Headers)
                {
                    message.Headers[key] = value;
                }
            }

            var response = await resendClient.EmailSendAsync(message, cancellationToken);

            if (!response.Success)
            {
                var failureKind = response.Exception?.IsTransient is true
                    ? FailureKind.Transient
                    : FailureKind.Permanent;

                return DeliveryOutcome.Failed(
                    envelope.NotificationId,
                    attemptId,
                    envelope.Channel,
                    ProviderName,
                    failureKind,
                    response.Exception?.Message ?? "Resend API returned a failure response.");
            }

            LogSent(logger, envelope.NotificationId, emailEnvelope.To, response.Content);

            return DeliveryOutcome.Succeeded(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                response.Content.ToString());
        }
        catch (ResendException ex) when (ex.IsTransient)
        {
            LogTransientFailure(logger, ex, envelope.NotificationId, emailEnvelope.To);

            return DeliveryOutcome.Failed(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                FailureKind.Transient,
                ex.Message);
        }
        catch (ResendException ex)
        {
            LogPermanentFailure(logger, ex, envelope.NotificationId, emailEnvelope.To);

            return DeliveryOutcome.Failed(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                FailureKind.Permanent,
                ex.Message);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Notification {NotificationId} has no EmailEnvelope payload. Cannot send via Resend.")]
    private static partial void LogMissingPayload(ILogger logger, NotificationId notificationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Notification {NotificationId} sent via Resend to {To}. Id: {ResendId}.")]
    private static partial void LogSent(
        ILogger logger,
        NotificationId notificationId,
        string to,
        Guid resendId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Transient Resend failure for {NotificationId} to {To}.")]
    private static partial void LogTransientFailure(
        ILogger logger,
        Exception exception,
        NotificationId notificationId,
        string to);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Permanent Resend failure for {NotificationId} to {To}.")]
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

    private ResendClient CreateResendClient(string apiKey)
    {
        var clientOptions = new ResendClientOptions
        {
            ApiToken = apiKey,
        };

        return new ResendClient(
            new StaticOptionsSnapshot<ResendClientOptions>(clientOptions),
            httpClientFactory.CreateClient(HttpClientName));
    }

    private sealed class StaticOptionsSnapshot<TOptions>(TOptions value) : IOptionsSnapshot<TOptions>
        where TOptions : class
    {
        public TOptions Value { get; } = value;

        public TOptions Get(string? name) => Value;
    }
}
