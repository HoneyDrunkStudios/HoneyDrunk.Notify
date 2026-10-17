using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.Providers.Email.Resend.DependencyInjection;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Net;
using System.Text.Json;

namespace HoneyDrunk.Notify.Tests.Providers;

/// <summary>Verifies the Resend HTTP boundary without delivering real email.</summary>
public sealed class ResendDeliveryTests
{
    /// <summary>Checks sender overrides, body format and provider identifiers.</summary>
    /// <param name="html">Whether to send HTML with an explicit sender.</param>
    /// <returns>The test task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuccessfulDeliveryPreservesEmailContent(bool html)
    {
        var id = Guid.NewGuid();
        using var handler = new RecordingHandler(HttpStatusCode.OK, JsonSerializer.Serialize(new { id }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("HoneyDrunk.Notify.Resend").Returns(client);
        var store = Substitute.For<ISecretStore>();
        store.GetSecretAsync(Arg.Any<SecretIdentifier>(), Arg.Any<CancellationToken>())
            .Returns(call => new SecretValue(call.Arg<SecretIdentifier>(), "test-api-key", version: null));
        using var provider = CreateProvider(factory, store);
        var sender = provider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Email);
        var envelope = Envelope() with
        {
            Payload = new EmailEnvelope("recipient@example.test", new EmailContent("Welcome", "Body", html))
            {
                From = html ? "override@example.test" : null,
                FromDisplayName = html ? "Override" : null,
                Headers = html ? new Dictionary<string, string> { ["X-Delivery"] = "example" } : null,
            },
        };

        var outcome = await sender.SendAsync(envelope);

        Assert.Equal(DeliveryStatus.Succeeded, outcome.Status);
        Assert.Equal(id.ToString(), outcome.ProviderMessageId);
        Assert.Equal("resend", outcome.Provider);
        Assert.Equal("Bearer test-api-key", handler.Authorization);
        using var body = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
        Assert.Equal("Welcome", body.RootElement.GetProperty("subject").GetString());
        Assert.Equal("Body", body.RootElement.GetProperty(html ? "html" : "text").GetString());
        Assert.Contains(html ? "override@example.test" : "default@example.test", body.RootElement.GetProperty("from").GetString(), StringComparison.Ordinal);
        Assert.Equal("recipient@example.test", body.RootElement.GetProperty("to")[0].GetString());
        if (html)
        {
            Assert.Equal("example", body.RootElement.GetProperty("headers").GetProperty("X-Delivery").GetString());
        }

        await store.Received(1).GetSecretAsync(Arg.Is<SecretIdentifier>(value => value.Name == "Resend--ApiKey"), Arg.Any<CancellationToken>());
    }

    /// <summary>Classifies provider failures so only retryable responses are retried.</summary>
    /// <param name="status">The HTTP response status.</param>
    /// <param name="expected">The expected failure class.</param>
    /// <param name="errorName">The provider error code.</param>
    /// <returns>The test task.</returns>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, FailureKind.Permanent, "validation_error")]
    [InlineData(HttpStatusCode.TooManyRequests, FailureKind.Transient, "rate_limit_exceeded")]
    [InlineData(HttpStatusCode.InternalServerError, FailureKind.Transient, "internal_server_error")]
    public async Task ProviderFailuresRetainRetryClassification(HttpStatusCode status, FailureKind expected, string errorName)
    {
        using var handler = new RecordingHandler(status, JsonSerializer.Serialize(new { name = errorName, message = "provider rejected request", statusCode = (int)status }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("HoneyDrunk.Notify.Resend").Returns(client);
        var store = Substitute.For<ISecretStore>();
        store.GetSecretAsync(Arg.Any<SecretIdentifier>(), Arg.Any<CancellationToken>())
            .Returns(call => new SecretValue(call.Arg<SecretIdentifier>(), "test-api-key", version: null));
        using var provider = CreateProvider(factory, store);
        var sender = provider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Email);

        var outcome = await sender.SendAsync(Envelope());

        Assert.Equal(DeliveryStatus.Failed, outcome.Status);
        Assert.Equal(expected, outcome.FailureKind);
        Assert.Contains("provider rejected request", outcome.ErrorMessage, StringComparison.Ordinal);
    }

    private static ServiceProvider CreateProvider(IHttpClientFactory factory, ISecretStore store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(store);
        services.AddHoneyDrunkNotifyResendProvider(options =>
        {
            options.FromAddress = "default@example.test";
            options.FromDisplayName = "Default";
        });
        services.AddSingleton(factory);
        return services.BuildServiceProvider();
    }

    private static NotificationEnvelope Envelope() => new(NotificationId.NewId(), NotificationChannel.Email, new Recipient(NotificationChannel.Email, "recipient@example.test"), new TemplateKey("welcome"), new Dictionary<string, object?>())
    {
        Payload = new EmailEnvelope("recipient@example.test", new EmailContent("Subject", "Body")),
    };

    private sealed class RecordingHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(status) { Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
