using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.ProviderSupport;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace HoneyDrunk.Notify.Providers.Email.Smtp;

/// <summary>
/// Sends email notifications via SMTP using <see cref="SmtpClient"/>.
/// Reads the rendered <see cref="EmailEnvelope"/> from <see cref="NotificationEnvelope.Payload"/>
/// so no template re-rendering occurs at the provider boundary.
/// </summary>
#pragma warning disable CA1812
internal sealed partial class SmtpNotificationSender(
    ISecretStore secretStore,
    IOptions<SmtpOptions> options,
    ILogger<SmtpNotificationSender> logger) : INotificationSender
#pragma warning restore CA1812
{
    private const string ProviderName = "smtp";
    private const string UsernameSecretName = "Smtp--Username";
    private const string PasswordSecretName = "Smtp--Password";

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

        var smtpOptions = options.Value;
        var fromAddress = emailEnvelope.From ?? smtpOptions.FromAddress;

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            return DeliveryOutcome.Failed(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName,
                FailureKind.Permanent,
                "No sender address configured. Set SmtpOptions.FromAddress or EmailEnvelope.From.");
        }

        try
        {
            using var message = BuildMailMessage(emailEnvelope, fromAddress, smtpOptions);
            var credentials = await GetCredentialsAsync(cancellationToken);
            using var client = CreateSmtpClient(smtpOptions, credentials);

            await client.SendMailAsync(message, cancellationToken);

            LogSent(logger, envelope.NotificationId, emailEnvelope.To);

            return DeliveryOutcome.Succeeded(
                envelope.NotificationId,
                attemptId,
                envelope.Channel,
                ProviderName);
        }
        catch (SmtpException ex) when (IsTransient(ex))
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
        catch (SmtpException ex)
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

    private static MailMessage BuildMailMessage(
        EmailEnvelope emailEnvelope, string fromAddress, SmtpOptions smtpOptions)
    {
        var fromDisplayName = emailEnvelope.FromDisplayName ?? smtpOptions.FromDisplayName;
        var from = new MailAddress(fromAddress, fromDisplayName);

        var message = new MailMessage(from, new MailAddress(emailEnvelope.To))
        {
            Subject = emailEnvelope.Content.Subject,
            Body = emailEnvelope.Content.Body,
            IsBodyHtml = emailEnvelope.Content.IsHtml,
        };

        if (emailEnvelope.Headers is not null)
        {
            foreach (var (key, value) in emailEnvelope.Headers)
            {
                message.Headers.Add(key, value);
            }
        }

        return message;
    }

    private static SmtpClient CreateSmtpClient(SmtpOptions smtpOptions, SmtpCredentials credentials)
    {
        var client = new SmtpClient(smtpOptions.Host, smtpOptions.Port)
        {
            EnableSsl = smtpOptions.UseSsl,
        };

        if (!string.IsNullOrWhiteSpace(credentials.Username))
        {
            client.Credentials = new NetworkCredential(credentials.Username, credentials.Password);
        }

        return client;
    }

    // 4xx SMTP status codes are typically transient (mailbox full, service unavailable)
    private static bool IsTransient(SmtpException ex) =>
        ex.StatusCode is SmtpStatusCode.ServiceNotAvailable
            or SmtpStatusCode.MailboxBusy
            or SmtpStatusCode.MailboxUnavailable
            or SmtpStatusCode.InsufficientStorage
            or SmtpStatusCode.ServiceClosingTransmissionChannel;

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Notification {NotificationId} has no EmailEnvelope payload. Cannot send via SMTP.")]
    private static partial void LogMissingPayload(ILogger logger, NotificationId notificationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Notification {NotificationId} sent via SMTP to {To}.")]
    private static partial void LogSent(ILogger logger, NotificationId notificationId, string to);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Transient SMTP failure for {NotificationId} to {To}.")]
    private static partial void LogTransientFailure(
        ILogger logger,
        Exception exception,
        NotificationId notificationId,
        string to);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Permanent SMTP failure for {NotificationId} to {To}.")]
    private static partial void LogPermanentFailure(
        ILogger logger,
        Exception exception,
        NotificationId notificationId,
        string to);

    private async Task<SmtpCredentials> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        var username = await secretStore.GetRequiredSecretValueAsync(UsernameSecretName, cancellationToken);
        var password = await secretStore.GetRequiredSecretValueAsync(PasswordSecretName, cancellationToken);
        return new SmtpCredentials(username, password);
    }

    private sealed record SmtpCredentials(string Username, string Password);
}
