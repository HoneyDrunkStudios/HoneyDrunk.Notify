// <copyright file="CoverageGateBackfillTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

using FluentAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.Abstractions.Models.Sms;
using HoneyDrunk.Notify.DependencyInjection;
using HoneyDrunk.Notify.Hosting.AspNetCore.ServiceCollectionExtensions;
using HoneyDrunk.Notify.Intake;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Providers.Email.Resend.DependencyInjection;
using HoneyDrunk.Notify.Providers.Email.Smtp.DependencyInjection;
using HoneyDrunk.Notify.Providers.Sms.Twilio.DependencyInjection;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Queue.AzureStorage.DependencyInjection;
using HoneyDrunk.Notify.Queue.InMemory.DependencyInjection;
using HoneyDrunk.Notify.Routing;
using HoneyDrunk.Notify.Storage;
using HoneyDrunk.Notify.Worker.Composition;
using HoneyDrunk.Notify.Worker.Options;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.Tests.Telemetry;

/// <summary>
/// Focused coverage backfill for public Notify contracts, routing, and provider registration guard paths.
/// </summary>
public sealed class CoverageGateBackfillTests
{
    /// <summary>
    /// Verifies strongly typed ULID identifiers round-trip valid values and reject invalid text.
    /// </summary>
    [Fact]
    public void StronglyTypedIdentifiers_RoundTripAndRejectInvalidText()
    {
        // Arrange
        var ulid = Ulid.NewUlid();

        // Act
        var notificationId = new NotificationId(ulid);
        var attemptId = new AttemptId(ulid.ToString());
        var parsedNotification = NotificationId.TryParse(ulid.ToString(), out var parsedNotificationId);
        var parsedAttempt = AttemptId.TryParse(ulid.ToString(), out var parsedAttemptId);
        var failedNotification = NotificationId.TryParse("not-a-ulid", out var failedNotificationId);
        var failedAttempt = AttemptId.TryParse("not-a-ulid", out var failedAttemptId);

        // Assert
        ((string)notificationId).Should().Be(ulid.ToString());
        notificationId.ToUlid().Should().Be(ulid);
        NotificationId.FromUlid(ulid).Should().Be(notificationId);
        parsedNotification.Should().BeTrue();
        parsedNotificationId.Should().Be(notificationId);
        failedNotification.Should().BeFalse();
        failedNotificationId.Should().Be(default(NotificationId));
        ((Ulid)attemptId).Should().Be(ulid);
        attemptId.ToUlid().Should().Be(ulid);
        AttemptId.FromUlid(ulid).Should().Be(attemptId);
        parsedAttempt.Should().BeTrue();
        parsedAttemptId.Should().Be(attemptId);
        failedAttempt.Should().BeFalse();
        failedAttemptId.Should().Be(default(AttemptId));
        Action invalidNotification = () => _ = new NotificationId("bad");
        Action invalidAttempt = () => _ = new AttemptId("bad");
        invalidNotification.Should().Throw<ArgumentException>();
        invalidAttempt.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies simple contract factories preserve status, failure, and provider details.
    /// </summary>
    [Fact]
    public void OutcomeFactories_PreserveProviderStatusAndFailureDetails()
    {
        // Arrange
        var notificationId = NotificationId.NewId();
        var attemptId = AttemptId.NewId();
        var acceptedAt = DateTimeOffset.Parse("2026-05-19T16:00:00Z", null, System.Globalization.DateTimeStyles.AssumeUniversal);

        // Act
        var succeeded = DeliveryOutcome.Succeeded(notificationId, attemptId, NotificationChannel.Email, "smtp", "provider-1");
        var failed = DeliveryOutcome.Failed(notificationId, attemptId, NotificationChannel.Sms, "twilio", FailureKind.Policy, "blocked");
        var deferred = DeliveryOutcome.Deferred(notificationId, attemptId, NotificationChannel.Email, "resend", "provider-2");
        var accepted = NotificationOutcome.Accepted(notificationId, acceptedAt);
        var rejected = NotificationOutcome.Rejected(notificationId, acceptedAt, RejectionReason.ValidationFailed, "bad recipient");

        // Assert
        succeeded.Status.Should().Be(DeliveryStatus.Succeeded);
        succeeded.FailureKind.Should().Be(FailureKind.None);
        succeeded.ProviderMessageId.Should().Be("provider-1");
        failed.Status.Should().Be(DeliveryStatus.Failed);
        failed.FailureKind.Should().Be(FailureKind.Policy);
        failed.ErrorMessage.Should().Be("blocked");
        deferred.Status.Should().Be(DeliveryStatus.Deferred);
        deferred.ProviderMessageId.Should().Be("provider-2");
        accepted.Status.Should().Be(NotificationAcceptanceStatus.Accepted);
        accepted.RejectionReason.Should().Be(RejectionReason.None);
        rejected.Status.Should().Be(NotificationAcceptanceStatus.Rejected);
        rejected.RejectionReason.Should().Be(RejectionReason.ValidationFailed);
        rejected.RejectionDetail.Should().Be("bad recipient");
    }

    /// <summary>
    /// Verifies value objects validate required address and idempotency constraints.
    /// </summary>
    [Fact]
    public void ValueObjects_ValidateRecipientAndIdempotencyConstraints()
    {
        // Act
        var recipient = Recipient.Email("person@example.test");
        var key = new IdempotencyKey("order-123");
        Action missingRecipient = () => Recipient.Email(" ");
        Action missingKey = () => _ = new IdempotencyKey(" ");
        Action longKey = () => _ = new IdempotencyKey(new string('x', 257));

        // Assert
        recipient.Channel.Should().Be(NotificationChannel.Email);
        recipient.Address.Should().Be("person@example.test");
        ((string)key).Should().Be("order-123");
        key.ToString().Should().Be("order-123");
        missingRecipient.Should().Throw<ArgumentException>();
        missingKey.Should().Throw<ArgumentException>();
        longKey.Should().Throw<ArgumentException>().WithMessage("*256*");
    }

    /// <summary>
    /// Verifies the in-memory intake queue preserves FIFO order and honors drain limits.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InMemoryNotificationEnqueuer_DrainsInFifoOrder()
    {
        // Arrange
        var enqueuer = new InMemoryNotificationEnqueuer();
        var first = Envelope(NotificationChannel.Email, "first@example.test");
        var second = Envelope(NotificationChannel.Email, "second@example.test");

        // Act
        await enqueuer.EnqueueAsync(first);
        await enqueuer.EnqueueAsync(second);
        var drained = await enqueuer.DrainAsync(1);
        var dequeued = enqueuer.TryDequeue(out var remaining);
        var empty = await enqueuer.DrainAsync(5);

        // Assert
        drained.Should().ContainSingle().Which.Should().Be(first);
        dequeued.Should().BeTrue();
        remaining.Should().Be(second);
        empty.Should().BeEmpty();
        Func<Task> nullEnvelope = () => enqueuer.EnqueueAsync(null!);
        await nullEnvelope.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies the idempotency store rejects active duplicates and reclaims expired keys.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InMemoryIdempotencyStore_RejectsDuplicatesUntilWindowExpires()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var now = DateTimeOffset.Parse("2026-05-19T16:00:00Z", null, System.Globalization.DateTimeStyles.AssumeUniversal);

        // Act
        var first = await store.TryBeginAsync("key", now, TimeSpan.FromMinutes(5));
        var duplicate = await store.TryBeginAsync("key", now.AddMinutes(1), TimeSpan.FromMinutes(5));
        var expired = await store.TryBeginAsync("key", now.AddMinutes(6), TimeSpan.FromMinutes(5));
        await store.CompleteAsync("key", "notification-1");

        // Assert
        first.Should().BeTrue();
        duplicate.Should().BeFalse();
        expired.Should().BeTrue();
        Func<Task> missingKey = () => store.TryBeginAsync(" ", now, TimeSpan.FromMinutes(5));
        Func<Task> missingCompleteKey = () => store.CompleteAsync(" ", "notification-1");
        Func<Task> missingNotification = () => store.CompleteAsync("key", " ");
        await missingKey.Should().ThrowAsync<ArgumentException>();
        await missingCompleteKey.Should().ThrowAsync<ArgumentException>();
        await missingNotification.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Verifies exponential backoff doubles delays and respects the configured cap.
    /// </summary>
    [Fact]
    public void ExponentialBackoffStrategy_CalculatesAndCapsDelays()
    {
        // Arrange
        var strategy = new ExponentialBackoffStrategy();

        // Act / Assert
        strategy.Calculate(0, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)).Should().Be(TimeSpan.FromSeconds(2));
        strategy.Calculate(2, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)).Should().Be(TimeSpan.FromSeconds(8));
        strategy.Calculate(10, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)).Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Verifies dispatcher terminal paths for success, permanent failure, deferred retry, and invalid retry configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task NotificationDispatcher_HandlesTerminalAndRetryOutcomes()
    {
        // Arrange
        var envelope = Envelope(NotificationChannel.Email, "person@example.test");
        var successSender = new SequenceSender(DeliveryOutcome.Succeeded(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake"));
        var permanentSender = new SequenceSender(DeliveryOutcome.Failed(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake", FailureKind.Permanent, "nope"));
        var retrySender = new SequenceSender(
            DeliveryOutcome.Deferred(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake"),
            DeliveryOutcome.Failed(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake", FailureKind.Transient, "try again"));

        // Act
        var success = await Dispatcher(successSender, maxAttempts: 1).DispatchAsync(envelope);
        var permanent = await Dispatcher(permanentSender, maxAttempts: 3).DispatchAsync(envelope);
        var exhausted = await Dispatcher(retrySender, maxAttempts: 2).DispatchAsync(envelope);
        Func<Task> invalid = () => Dispatcher(successSender, maxAttempts: 0).DispatchAsync(envelope);

        // Assert
        success.Status.Should().Be(DeliveryStatus.Succeeded);
        permanent.FailureKind.Should().Be(FailureKind.Permanent);
        exhausted.FailureKind.Should().Be(FailureKind.Transient);
        retrySender.Calls.Should().Be(2);
        await invalid.Should().ThrowAsync<InvalidOperationException>().WithMessage("*MaxAttempts*");
    }

    /// <summary>
    /// Verifies gateway validation, disabled runtime, duplicate idempotency, and accepted email payload paths.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task NotificationGateway_ValidatesDedupesAndBuildsEmailPayloads()
    {
        // Arrange
        var enqueuer = new InMemoryNotificationEnqueuer();
        var store = new InMemoryIdempotencyStore();
        var renderer = new StubEmailRenderer();
        var gateway = new NotificationGateway(
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions { DedupeWindow = TimeSpan.FromMinutes(5) }),
            enqueuer,
            store,
            renderer,
            NullLogger<NotificationGateway>.Instance);
        var request = Request("person@example.test") with { IdempotencyKey = new IdempotencyKey("same-key") };

        // Act
        var accepted = await gateway.EnqueueAsync(request);
        var duplicate = await gateway.EnqueueAsync(request);
        var invalid = await gateway.EnqueueAsync(Request("not-an-email"));
        var disabled = await new NotificationGateway(
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions { Enabled = false }),
            enqueuer,
            store,
            renderer,
            NullLogger<NotificationGateway>.Instance).EnqueueAsync(Request("person@example.test"));
        enqueuer.TryDequeue(out var envelope).Should().BeTrue();

        // Assert
        accepted.Status.Should().Be(NotificationAcceptanceStatus.Accepted);
        duplicate.RejectionReason.Should().Be(RejectionReason.DuplicateIdempotencyKey);
        invalid.RejectionReason.Should().Be(RejectionReason.ValidationFailed);
        disabled.RejectionReason.Should().Be(RejectionReason.RuntimeDisabled);
        envelope!.Payload.Should().BeOfType<EmailEnvelope>()
            .Which.Content.Subject.Should().Be("Rendered subject");
        envelope.IdempotencyKey.Should().Be(new IdempotencyKey("same-key"));
        envelope.Tags.Should().Contain("critical");
    }

    /// <summary>
    /// Verifies provider registration resolves keyed senders and safe guard paths without external service calls.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ProviderRegistrations_ResolveSendersAndRejectInvalidPayloadsWithoutExternalCalls()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());
        services.AddHoneyDrunkNotifyRuntime();
        services.AddHoneyDrunkNotifySmtpProvider(options => options.FromAddress = "smtp@example.test");
        services.AddHoneyDrunkNotifyResendProvider(options => options.FromAddress = "resend@example.test");
        services.AddHoneyDrunkNotifyTwilioProvider(options => options.FromNumber = "+15551234567");
        using var provider = services.BuildServiceProvider();
        var emailEnvelope = Envelope(NotificationChannel.Email, "person@example.test") with { Payload = new SmsEnvelope("+15550000000", "wrong") };
        var smsEnvelope = Envelope(NotificationChannel.Sms, "+15550000000") with { Payload = new EmailEnvelope("person@example.test", new EmailContent("s", "b")) };

        // Act
        var emailSender = provider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Email);
        var smsSender = provider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Sms);
        var emailResult = await emailSender.SendAsync(emailEnvelope);
        var smsResult = await smsSender.SendAsync(smsEnvelope);

        // Assert
        emailResult.Status.Should().Be(DeliveryStatus.Failed);
        emailResult.FailureKind.Should().Be(FailureKind.Permanent);
        smsResult.Status.Should().Be(DeliveryStatus.Failed);
        smsResult.FailureKind.Should().Be(FailureKind.Permanent);
        provider.GetRequiredService<INotificationSender>().Should().BeSameAs(emailSender);
    }

    /// <summary>
    /// Verifies hosting and worker composition map options and register queue/provider services.
    /// </summary>
    [Fact]
    public void HostingAndWorkerComposition_RegisterExpectedRuntimeServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());

        // Act
        services.AddHoneyDrunkNotify(options =>
        {
            options.Enabled = false;
            options.Retry.MaxAttempts = 7;
            options.Retry.BaseDelay = TimeSpan.FromSeconds(1);
            options.Retry.MaxDelay = TimeSpan.FromSeconds(9);
            options.Policy.EnableDedupe = false;
            options.Policy.DedupeWindow = TimeSpan.FromSeconds(30);
            options.Templates.RootPath = "custom-templates";
            options.Templates.Extension = ".liquid";
            options.Templates.CacheEnabled = false;
            options.Templates.CacheTtl = TimeSpan.FromSeconds(5);
        });
        services.AddHoneyDrunkNotifyInMemoryQueue();
        services.AddHoneyDrunkNotifyWorker(options => options.QueueAdapter = "InMemory");
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<IOptions<NotifyRuntimeOptions>>().Value;
        var templates = provider.GetRequiredService<IOptions<HoneyDrunk.Notify.Options.TemplateOptions>>().Value;
        var worker = provider.GetRequiredService<IOptions<NotifyWorkerOptions>>().Value;

        // Assert
        runtime.Enabled.Should().BeFalse();
        runtime.MaxAttempts.Should().Be(7);
        runtime.EnableDedupe.Should().BeFalse();
        templates.RootPath.Should().Be("custom-templates");
        templates.Extension.Should().Be(".liquid");
        templates.CacheEnabled.Should().BeFalse();
        worker.QueueAdapter.Should().Be("InMemory");
        provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies each provider fails closed when the payload is valid but no sender address is configured.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ProviderSenders_RejectMissingSenderConfigurationBeforeExternalCalls()
    {
        // Arrange
        var smtpSender = BuildSender(services => services.AddHoneyDrunkNotifySmtpProvider());
        var resendSender = BuildSender(services => services.AddHoneyDrunkNotifyResendProvider(_ => { }));
        var twilioSender = BuildSender(services => services.AddHoneyDrunkNotifyTwilioProvider(_ => { }), NotificationChannel.Sms);
        var email = Envelope(NotificationChannel.Email, "person@example.test") with
        {
            Payload = new EmailEnvelope("person@example.test", new EmailContent("Subject", "Body")),
        };
        var sms = Envelope(NotificationChannel.Sms, "+15550000000") with
        {
            Payload = new SmsEnvelope("+15550000000", "Hello"),
        };

        // Act
        var smtp = await smtpSender.SendAsync(email);
        var resend = await resendSender.SendAsync(email);
        var twilio = await twilioSender.SendAsync(sms);

        // Assert
        smtp.Provider.Should().Be("smtp");
        smtp.Status.Should().Be(DeliveryStatus.Failed);
        smtp.ErrorMessage.Should().Contain("No sender address configured");
        resend.Provider.Should().Be("resend");
        resend.Status.Should().Be(DeliveryStatus.Failed);
        resend.ErrorMessage.Should().Contain("No sender address configured");
        twilio.Provider.Should().Be("twilio");
        twilio.Status.Should().Be(DeliveryStatus.Failed);
        twilio.ErrorMessage.Should().Contain("No sender phone number configured");
    }

    /// <summary>
    /// Verifies provider and queue options expose default values and retain configured values.
    /// </summary>
    [Fact]
    public void ProviderAndQueueOptions_ExposeDefaultsAndConfiguredValues()
    {
        // Arrange / Act
        var smtp = new HoneyDrunk.Notify.Providers.Email.Smtp.SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 2525,
            UseSsl = true,
            FromAddress = "from@example.test",
            FromDisplayName = "HoneyDrunk",
        };
        var resend = new HoneyDrunk.Notify.Providers.Email.Resend.ResendOptions
        {
            FromAddress = "resend@example.test",
            FromDisplayName = "Resend",
        };
        var twilio = new HoneyDrunk.Notify.Providers.Sms.Twilio.TwilioOptions
        {
            FromNumber = "+15551234567",
        };
        var queue = new HoneyDrunk.Notify.Queue.AzureStorage.AzureStorageQueueOptions
        {
            QueueName = "notify-main",
            DeadLetterQueueName = "notify-dlq",
            ConnectionStringSecretName = "QueueSecret",
            CreateIfNotExists = false,
            MaxBatchSize = 16,
            MaxDeliveryAttempts = 9,
        };
        var notificationQueue = new NotificationQueueOptions
        {
            QueueName = "notify",
            DeadLetterQueueName = null,
        };

        // Assert
        smtp.Host.Should().Be("smtp.example.test");
        smtp.Port.Should().Be(2525);
        smtp.UseSsl.Should().BeTrue();
        smtp.FromAddress.Should().Be("from@example.test");
        smtp.FromDisplayName.Should().Be("HoneyDrunk");
        resend.FromAddress.Should().Be("resend@example.test");
        resend.FromDisplayName.Should().Be("Resend");
        twilio.FromNumber.Should().Be("+15551234567");
        queue.QueueName.Should().Be("notify-main");
        queue.EffectiveDeadLetterQueueName.Should().Be("notify-dlq");
        queue.ConnectionStringSecretName.Should().Be("QueueSecret");
        queue.CreateIfNotExists.Should().BeFalse();
        queue.MaxBatchSize.Should().Be(16);
        queue.MaxDeliveryAttempts.Should().Be(9);
        notificationQueue.EffectiveDeadLetterQueueName.Should().Be("notify-dlq");
    }

    /// <summary>
    /// Verifies Azure Storage queue registration exposes both queue and dead-letter abstractions.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AzureStorageQueueRegistration_ExposesQueueAndDeadLetterInspector()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());

        // Act
        services.AddHoneyDrunkNotifyAzureStorageQueue(options =>
        {
            options.ConnectionString = "UseDevelopmentStorage=true";
            options.QueueName = "notify";
        });
        await using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<INotificationQueue>().Should().NotBeNull();
        provider.GetRequiredService<IDeadLetterInspector>().Should().NotBeNull();
    }

    private static INotificationSender BuildSender(
        Action<IServiceCollection> register,
        NotificationChannel channel = NotificationChannel.Email)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());
        services.AddHoneyDrunkNotifyRuntime();
        register(services);
        return services.BuildServiceProvider().GetRequiredKeyedService<INotificationSender>(channel);
    }

    private static NotificationDispatcher Dispatcher(SequenceSender sender, int maxAttempts) =>
        new(
            new StubResolver(sender),
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions
            {
                MaxAttempts = maxAttempts,
                BaseDelay = TimeSpan.Zero,
                MaxDelay = TimeSpan.Zero,
            }),
            new ExponentialBackoffStrategy(),
            TimeProvider.System,
            NullLogger<NotificationDispatcher>.Instance);

    private static NotificationRequest Request(string address) =>
        new(NotificationChannel.Email, Recipient.Email(address), new TemplateKey("welcome"), new Dictionary<string, object?> { ["name"] = "Oleg" })
        {
            Priority = NotificationPriority.High,
            Tags = ["critical"],
        };

    private static NotificationEnvelope Envelope(NotificationChannel channel, string address) =>
        new(NotificationId.NewId(), channel, new Recipient(channel, address), new TemplateKey("welcome"), new Dictionary<string, object?>())
        {
            CorrelationId = "corr-1",
            TenantId = "tenant-1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

    private sealed class StubEmailRenderer : IEmailTemplateRenderer
    {
        public Task<EmailContent> RenderEmailAsync(
            TemplateKey templateKey,
            IReadOnlyDictionary<string, object?> model,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmailContent("Rendered subject", "Rendered body"));
    }

    private sealed class StubResolver(INotificationSender sender) : INotificationSenderResolver
    {
        public INotificationSender Resolve(NotificationChannel channel) => sender;
    }

    private sealed class SequenceSender(params DeliveryOutcome[] outcomes) : INotificationSender
    {
        public int Calls { get; private set; }

        public Task<DeliveryOutcome> SendAsync(NotificationEnvelope envelope, CancellationToken cancellationToken = default)
        {
            var index = Math.Min(Calls, outcomes.Length - 1);
            Calls++;
            return Task.FromResult(outcomes[index]);
        }
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        public Task<SecretValue> GetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SecretValue(identifier, "secret-value", version: null));

        public Task<VaultResult<SecretValue>> TryGetSecretAsync(
            SecretIdentifier identifier,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SecretVersion>> ListSecretVersionsAsync(
            string secretName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
